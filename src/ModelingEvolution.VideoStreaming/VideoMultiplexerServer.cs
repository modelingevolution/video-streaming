using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using EventPi.Abstractions;
using Makaretu.Dns;
using MicroPlumberd;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelingEvolution.VideoStreaming.Events;

namespace ModelingEvolution.VideoStreaming;

public class VideoStreamingServer : INotifyPropertyChanged
{

    public event EventHandler<VideoAddress> SourceStreamConnected;
    public event EventHandler<VideoAddress> SourceStreamDisconnected;

    public IReadOnlyCollection<VideoAddress> DisconnectedSources => _disconnected;
    private readonly ObservableCollection<VideoAddress> _disconnected;
    private readonly ObservableCollection<IVideoStreamReplicator> _streams;
    private readonly string _host;
    private readonly int _port;
    private readonly string _protocol;
    private readonly string[] _tags;
    private readonly ILoggerFactory _loggerFactory;
    private readonly TcpListener _listener;
    private readonly ServerConfigProvider _configProvider;
    private readonly IPlumber _plumber;
    private readonly IPartialMatFrameHandler[] _matHandlers;
    private readonly IPartialYuvFrameHandler[] _yuvHandlers;
    private readonly VideoStreamEventSink _sink;
    private readonly IEnvironment _env;

    private DateTime _started;
    public bool HasManySourceStreams => _streams.Count > 1;
    public State State
    {
        get => _state;
        private set => SetField(ref _state, value);
    }

    public string Host => _host;
    public async Task Delete(VideoAddress address)
    {
        for (int i = 0; i < _streams.Count; i++) 
        {
            var r = _streams[i];
            if (r.VideoAddress.Equals(address))
            {
                await RemoveConfig(r.VideoAddress);
                r.Stop();
                break;
            }
        }
    }
    public int Port => _port;
    public IList<IVideoStreamReplicator> Streams => _streams;
    private readonly ILogger<VideoStreamingServer> _logger;
    public ServerConfigProvider ServerConfigProvider => _configProvider;
    public VideoStreamingServer(string host, int port, ILogger<VideoStreamingServer> logger,
        IPlumber plumber, bool isSingleVideo, 
        VideoStreamEventSink sink,
        IEnvironment env,
        IConfiguration config,
        ILoggerFactory loggerFactory, 
        IEnumerable<IPartialMatFrameHandler> matHandlers,
        IEnumerable<IPartialYuvFrameHandler> yuvHandlers)
    {
        _host = host;
        _port = port;
        _loggerFactory = loggerFactory;
        IsSingleVideoSource = isSingleVideo;
        _plumber = plumber;
        _matHandlers = matHandlers.ToArray();
        _yuvHandlers = yuvHandlers.ToArray();
        _sink = sink;
        _env = env;
        if (host == "localhost") 
            _host = IPAddress.Loopback.ToString();

        _listener = new TcpListener(IPAddress.Parse(_host), _port)
        {
            Server =
            {
                NoDelay = true, 
                SendTimeout = 30*1000,
                ReceiveTimeout = 30*1000
            }
        };
        _streams = new ObservableCollection<IVideoStreamReplicator>();
        _configProvider = new ServerConfigProvider(config, plumber, env, loggerFactory.CreateLogger<ServerConfigProvider>());
        _logger = logger;
        _disconnected = new ObservableCollection<VideoAddress>();
        NxReconnect = DateTime.Now;
    }
  
    public async Task<IVideoStreamReplicator> ConnectVideoSource(VideoAddress va)
    {
        var rep = _streams.FirstOrDefault(x => x.VideoAddress == va);
        if (rep != null)
            return rep;

        var streamReplicator = OnConnectVideoSource(va);
        await SaveConfig(va);
        return streamReplicator;
    }

    private async Task SaveConfig(VideoAddress va)
    {
        var config = await _configProvider.Get();

        config.Sources.Add(va.Uri);
        await _configProvider.Save();
    }
    private async Task RemoveConfig(VideoAddress va)
    {
        var config = await _configProvider.Get();

        var s = config.Sources.RemoveAll(x => VideoAddress.CreateFrom(x).Equals(va));

        await _configProvider.Save();
    }

    private IVideoStreamReplicator OnConnectVideoSource(VideoAddress va)
    {
        IVideoStreamReplicator streamReplicator = va.VideoTransport == VideoTransport.Tcp ?
            new VideoStreamReplicator(va, _loggerFactory, _sink):
            new VideoSharedBufferReplicator(va,
                (FrameInfo)va.Resolution,
                _sink,
                _loggerFactory.CreateLogger<VideoSharedBufferReplicator>(), 
                _loggerFactory, _matHandlers, _yuvHandlers);
        try
        {
            _streams.Add(streamReplicator.Connect());
            SourceStreamConnected?.Invoke(this,va);
            streamReplicator.Stopped += OnReplicatorStopped;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Cannot connect to video source {Address}", va);
            streamReplicator.Dispose();
            throw;
        }

        return streamReplicator;
    }

    private void OnReplicatorStopped(object? sender, StoppedEventArgs e)
    {
        var replicator = (IVideoStreamReplicator)sender;
        OnDisconnectReplicator(replicator, e.Reason);
    }

    private void OnDisconnectReplicator(IVideoStreamReplicator replicator, StoppedReason reason)
    {
        replicator.Stopped -= OnReplicatorStopped;
        _streams.Remove(replicator);

        OnDisconnectVideoSource(replicator.VideoAddress, true, reason);
        replicator.Dispose();
        SourceStreamDisconnected?.Invoke(this, replicator.VideoAddress);
    }

    public DateTime? Started
    {
        get
        {
            if (State != State.Stopped && State != State.Initialized && State != State.Failed)
                return _started;
            return null;
        }
    }

    public DateTime NxReconnect { get; set; }

    public void Start()
    {
        _logger.LogInformation("Video stream replicator is starting...");
        State = State.Starting;
      
        
        try
        {
            _started = DateTime.Now;
            _tokenSource = new CancellationTokenSource();
            _listener.Start();

            Task.Run(OnAcceptEx);
            Task.Run(OnAutoReconnectLoop);
            
            State = State.Running;
            _logger.LogInformation("Video stream replicator is running.");

            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Video stream replicator failed to start");
            State = State.Failed;
        }
    }

    private bool TryConnectVideoSource(VideoAddress va)
    {
        try
        {

            this.OnConnectVideoSource( va);
            this._disconnected.SafeRemove(va);

            _ = _sink.OnStreamingStarted(va);

            return true;
        }
        catch (Exception ex)
        {
            OnDisconnectVideoSource(va, false);
        }

        return false;
    }

    public async Task LoadConfig()
    {
        var serverConfig = await _configProvider.Get();
        foreach (var i in serverConfig.Sources.Distinct()) 
            TryConnectVideoSource(VideoAddress.CreateFrom(i));
    }

    const int HANDSHAKE_TIMEOUTS_MS = 10000;

    public IVideoStreamReplicator? GetReplicator(string? name)
    {
        if (name == null) return _streams.FirstOrDefault();

        var replicator = _streams.FirstOrDefault(x => x.Is(name));
        return replicator;
    }
    private async Task<IVideoStreamReplicator?> FindReplicator(StreamBase ns)
    {
        var toRead = await ns.ReadByteAsync(HANDSHAKE_TIMEOUTS_MS);
        if (toRead.IsNaN) return null;

        var host = await ns.ReadAsciiStringAsync(toRead, HANDSHAKE_TIMEOUTS_MS);
        if (host == null) return null;

        var replicator = _streams.FirstOrDefault(x => x.Is(host));

        if (replicator != null)
        {
            _logger.LogInformation("Video stream found that started at {Started} from {Name}", 
                replicator.Started,replicator.VideoAddress);
            return replicator;
        }


        _logger.LogWarning("Cannot find stream for {RequestedStream}", host);
        return null;
    }

    private bool TryFindSource(NetworkStream ns, out IVideoStreamReplicator replicator)
    {
        var bytes = new byte[1024];
        int read = ns.Read(bytes);

        string requestedStream = Encoding.UTF8.GetString(bytes[..read]);

        replicator = _streams.FirstOrDefault(row => row.VideoAddress.Host == requestedStream);

        if (replicator is not null)
        {
            return true;
        }

        _logger.LogWarning("Cannot find stream for {RequestedStream}", requestedStream);
        return false;
    }

    private CancellationTokenSource _tokenSource;
    private State _state;
    
    public bool IsSingleVideoSource { get; }
    public bool IsReconnecting { get; private set; }

    

    public async Task OnAutoReconnectLoop()
    {
        while (true)
        {
            if (_tokenSource.IsCancellationRequested) return;
            try
            {
                if (_disconnected.Count > 0)
                {
                    ReconnectAll();
                }
                var dt = TimeSpan.FromSeconds(30);
                NxReconnect = DateTime.Now.Add(dt);
                await Task.Delay(dt, _tokenSource.Token);
            }
            catch (OperationCanceledException ex)
            {
                return;
            }
        }
    }

    private void ReconnectAll()
    {
        IsReconnecting = true;
        lock (_disconnected) // a bit wide.
        {
            for (int i = 0; i < _disconnected.Count; i++)
            {
                var videoSource = _disconnected[i];
                _logger.LogInformation(
                    $"Trying to reconnect video source: {videoSource}");
                if (!TryConnectVideoSource(videoSource)) continue;

                _logger.LogInformation($"Connected: {videoSource}");
                i -= 1;
            }
        }

        IsReconnecting = false;
    }
    private async Task OnAcceptEx()
    {
        while (true)
        {
            try
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(_tokenSource.Token);
                var ns = client.GetStream().AsNonBlocking();

                if (IsSingleVideoSource)
                {
                    var stream = _streams.FirstOrDefault();
                    if (stream != default)
                    {
                        stream.ReplicateTo(ns.Stream, client?.Client?.RemoteEndPoint?.ToString());
                    }
                    else
                    {
                        await ns.DisposeAsync();
                        client.Dispose();
                    }
                }
                else
                {
                    var source = await FindReplicator(ns);
                    if (source != null)
                        source.ReplicateTo(ns.Stream, client?.Client?.RemoteEndPoint?.ToString());
                    else
                    {
                        await ns.DisposeAsync();
                        client.Dispose();
                    }
                }


            }
            catch (OperationCanceledException ex)
            {
                State = State.Stopped;
                break;
            }
        }
    }
    private async Task OnAccept()
    {
        while (true)
        {
            try
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(_tokenSource.Token);
                NetworkStream ns = client.GetStream();

                if (TryFindSource(ns, out IVideoStreamReplicator source))
                {
                    source.ReplicateTo(ns, client.Client.RemoteEndPoint.ToString());
                }
                else
                {
                    await ns.DisposeAsync();
                    client.Dispose();
                }
            }
            catch (OperationCanceledException ex)
            {
                State = State.Stopped;
                break;
            }
        }
    }

    public void Stop()
    {
        var tcpMultiplexerStopped = new MultiplexerStopped();
        _plumber.AppendEvent(tcpMultiplexerStopped, _env.HostName);

        _logger.LogInformation("Video stream replicator is shutting down.");
        this._tokenSource.Cancel();
        foreach (var i in _streams)
            i.Dispose();
        _streams.Clear();
        _listener.Stop();
        _logger.LogInformation("Video stream replicator is stopped.");

    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnDisconnectVideoSource(VideoAddress videoSource, bool saveEvent = false, StoppedReason r = StoppedReason.ConnectionDisconnected)
    {
        if (saveEvent)
        {
            var streamingStopped = new StreamingStopped
            {
                Source = videoSource.Host
            };

            _plumber.AppendEvent(streamingStopped, _env.HostName);
        }
        if(r == StoppedReason.ConnectionDisconnected) 
            _disconnected.SafeAddUnique(videoSource);
    }

    
}
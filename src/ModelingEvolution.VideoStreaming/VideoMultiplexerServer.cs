using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using Makaretu.Dns;
using MicroPlumberd;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelingEvolution.VideoStreaming.Events;

namespace ModelingEvolution.VideoStreaming;

public readonly struct VideoAddress
{
    public string? StreamName { get; init; }
    public string Host { get; init; }
    public int Port { get; init; }
    public string Protocol { get; init; }
    public VideoAddress(string protocol, string host, int port, string? streamName = null)
    {
        Host = host;
        Port = port;
        StreamName = streamName;
        Protocol = protocol;
    }
   
}
static class CollectionExtensions
{
    public static void SafeAddUnique<T>(this IList<T> collection, T item)
    {
        lock (collection)
        {
            if (!collection.Contains(item))
                collection.Add(item);
        }
    }

    public static bool SafeRemove<T>(this IList<T> collection, T item)
    {
        lock (collection)
        {
            return collection.Remove(item);
        }
    }
}

public class VideoStreamingServer : INotifyPropertyChanged
{
    public record VideoSource(string Protocol, string Host, int Port, string StreamName, params string[] Tags) 
    {
        public static VideoSource CreateFrom(Uri i)
        {
            var proto = i.Scheme;
            var host = i.Host;
            var port = i.Port;
            var path = i.PathAndQuery.TrimStart('/').Split(',');
            var streamName = path.Any() ? path[0] : host;
            var tags = path.Length > 1 ? path.Skip(1).ToArray() : Array.Empty<string>();
            return new VideoSource(proto, host, port,  streamName, tags);
        }
        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(StreamName))
                return $"{Protocol}://{Host}:{Port}/{StreamName},{string.Join(',', Tags)}";
            else
                return $"{Protocol}://{Host}:{Port}";
        }
    }

    public IReadOnlyCollection<VideoSource> DisconnectedSources => _disconnected;
    private readonly ObservableCollection<VideoSource> _disconnected;
    private readonly ObservableCollection<VideoStreamReplicator> _streams;
    private readonly string _host;
    private readonly int _port;
    private readonly string _protocol;
    private readonly string[] _tags;
    private readonly ILoggerFactory _loggerFactory;
    private readonly TcpListener _listener;
    private readonly ServerConfigProvider _configProvider;
    private readonly IPlumber _plumber;
    private ServiceDiscovery _serviceDiscovery;
    private DateTime _started;
    
    public State State
    {
        get => _state;
        private set => SetField(ref _state, value);
    }

    public string Host => _host;
    public string Protocol => _protocol;
    public int Port => _port;
    public IList<VideoStreamReplicator> Streams => _streams;
    private readonly ILogger<VideoStreamingServer> _logger;
    public ServerConfigProvider ServerConfigProvider => _configProvider;
    public VideoStreamingServer(string host, int port, ILogger<VideoStreamingServer> logger,
        IPlumber plumber, bool isSingleVideo, IConfiguration config, ILoggerFactory loggerFactory)
    {
        _host = host;
        _port = port;
        _loggerFactory = loggerFactory;
        IsSingleVideoSource = isSingleVideo;
        _plumber = plumber;
        if (host == "localhost")
        {
            _host = IPAddress.Loopback.ToString();
        }

        _listener = new TcpListener(IPAddress.Parse(_host), _port)
        {
            Server =
            {
                NoDelay = true, 
                SendTimeout = 30*1000,
                ReceiveTimeout = 30*1000
            }
        };
        _streams = new ObservableCollection<VideoStreamReplicator>();
        _configProvider = new ServerConfigProvider(config, plumber);
        _logger = logger;
        _disconnected = new ObservableCollection<VideoSource>();
        NxReconnect = DateTime.Now;
    }
    public async Task<VideoStreamReplicator> ConnectVideoSource(VideoSource src)
    {
        return await ConnectVideoSource(src.Protocol, src.Host, src.Port, src.StreamName, src.Tags);
    }
    public async Task<VideoStreamReplicator> ConnectVideoSource(string protocol, string host, int port, string streamName, params string[] tags)
    {
        var streamReplicator = OnConnectVideoSource(protocol,host, port, streamName, tags);
        await SaveConfig(protocol,host, port, streamName, tags);
        return streamReplicator;
    }

    private async Task SaveConfig(string protocol, string host, int port, string streamName, params string[] tags)
    {
        var config = await _configProvider.Get();
        string tagsSufix = tags.Any() ? $",{string.Join(',', tags)}" : "";

        config.Sources.Add(new Uri($"{protocol}://{host}:{port}/{streamName}{tagsSufix}"));
        await _configProvider.Save();
    }

    
    private VideoStreamReplicator OnConnectVideoSource(string protocol, string host, int port, string streamName, string[] tags)
    {
        var streamReplicator = new VideoStreamReplicator(protocol,host, port, streamName, tags, _loggerFactory, _plumber);
        try
        {
            _streams.Add(streamReplicator.Connect());
            streamReplicator.Stopped += OnReplicatorStopped;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Cannot connect to video source {Host}:{Port}", host, port);
            streamReplicator.Dispose();
            throw;
        }

        return streamReplicator;
    }

    private void OnReplicatorStopped(object? sender, EventArgs e)
    {
        VideoStreamReplicator replicator = (VideoStreamReplicator)sender;
        replicator.Stopped -= OnReplicatorStopped;
        _streams.Remove(replicator);

        var videoSource = new VideoSource(replicator.Protocol,replicator.Host, replicator.Port, replicator.StreamName);
        OnDisconnectVideoSource(videoSource, true);

        replicator.Dispose();
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

    public void Start(bool? advertise = null)
    {
        _logger.LogInformation("Video stream replicator is starting...");
        State = State.Starting;
        if (advertise.HasValue)
            this.Advertise = true;
        
        try
        {
            _started = DateTime.Now;
            _tokenSource = new CancellationTokenSource();
            _listener.Start();

            Task.Run(OnAcceptEx);
            Task.Run(OnAutoReconnectLoop);
            
            State = State.Running;
            _logger.LogInformation("Video stream replicator is running.");

            if (!Advertise) return;

            var videoSrv = new ServiceProfile(Dns.GetHostName(), "video.tcp", (ushort)Port);
            var eventStoreSrv = new ServiceProfile(Dns.GetHostName(), "eventStore.tcp", 2113);

            _serviceDiscovery = new ServiceDiscovery();
            _serviceDiscovery.Advertise(videoSrv);
            _serviceDiscovery.Advertise(eventStoreSrv);

            _logger.LogInformation("Advertising tcp service");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Video stream replicator failed to start");
            State = State.Failed;
        }
    }

    private bool TryConnectVideoSource(VideoSource vs)
    {
        try
        {

            this.OnConnectVideoSource( vs.Protocol,vs.Host, vs.Port, vs.StreamName, vs.Tags);
            this._disconnected.SafeRemove(vs);

            var streamingStarted = new StreamingStarted
            {
                Source = vs.Host
            };

            _plumber.AppendEvent( streamingStarted, vs.Host);

            return true;
        }
        catch (Exception ex)
        {
            OnDisconnectVideoSource(vs, false);
        }

        return false;
    }

    public async Task LoadConfig()
    {
        var serverConfig = await _configProvider.Get();
        foreach (var i in serverConfig.Sources.Distinct()) 
            TryConnectVideoSource(VideoSource.CreateFrom(i));
    }

    const int HANDSHAKE_TIMEOUTS_MS = 10000;

    public VideoStreamReplicator? GetReplicator(string? name)
    {
        if (name == null) return _streams.FirstOrDefault();

        var replicator =
            _streams.FirstOrDefault(row => row.Host.Equals(name, StringComparison.CurrentCultureIgnoreCase) ||
                                           string.Equals(row.StreamName, name, StringComparison.CurrentCultureIgnoreCase) ||
                                           row.Tags.Contains(name));
        return replicator;
    }
    private async Task<VideoStreamReplicator?> FindReplicator(StreamBase ns)
    {
        var toRead = await ns.ReadByteAsync(HANDSHAKE_TIMEOUTS_MS);
        if (toRead.IsNaN) return null;

        var host = await ns.ReadAsciiStringAsync(toRead, HANDSHAKE_TIMEOUTS_MS);
        if (host == null) return null;

        var replicator =
            _streams.FirstOrDefault(row => row.Host.Equals(host, StringComparison.CurrentCultureIgnoreCase) || 
                                           string.Equals(row.StreamName, host, StringComparison.CurrentCultureIgnoreCase) ||
                                           row.Tags.Contains(host));

        if (replicator != null)
        {
            _logger.LogInformation("Video stream found that started at {Started} from {HostName} at {Port} ({StreamName}).", 
                replicator.Started, 
                replicator.Host, 
                replicator.Port, 
                replicator.StreamName ?? "-");
            return replicator;
        }


        _logger.LogWarning("Cannot find stream for {RequestedStream}", host);
        return null;
    }

    private bool TryFindSource(NetworkStream ns, out VideoStreamReplicator replicator)
    {
        var bytes = new byte[1024];
        int read = ns.Read(bytes);

        string requestedStream = Encoding.UTF8.GetString(bytes[..read]);

        replicator = _streams.FirstOrDefault(row => row.Host == requestedStream);

        if (replicator is not null)
        {
            return true;
        }

        _logger.LogWarning("Cannot find stream for {RequestedStream}", requestedStream);
        return false;
    }

    private CancellationTokenSource _tokenSource;
    private State _state;
    private bool _advertise;
    public bool IsSingleVideoSource { get; }
    public bool IsReconnecting { get; private set; }

    public bool Advertise
    {
        get => _advertise;
        set => SetField(ref _advertise, value);
    }

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
                    $"Trying to reconnect video source: {videoSource.Host}:{videoSource.Port}");
                if (!TryConnectVideoSource(videoSource)) continue;

                _logger.LogInformation($"Connected: {videoSource.Host}:{videoSource.Port}");
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

                if (TryFindSource(ns, out VideoStreamReplicator source))
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
        _plumber.AppendEvent(tcpMultiplexerStopped, _host);

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

    private void OnDisconnectVideoSource(VideoSource videoSource, bool saveEvent = false)
    {
        if (saveEvent)
        {
            var streamingStopped = new StreamingStopped
            {
                Source = videoSource.Host
            };

            _plumber.AppendEvent(streamingStopped, videoSource.Host);
        }

        _disconnected.SafeAddUnique(videoSource);
    }

    
}
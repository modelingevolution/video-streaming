using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using MicroPlumberd;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ModelingEvolution.VideoStreaming.Events;
using ModelingEvolution.VideoStreaming.Nal;
using OpenCvSharp;
using static OpenCvSharp.LineIterator;

#pragma warning disable CS4014


namespace ModelingEvolution.VideoStreaming
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public unsafe struct FullHDFrame 
    {
        public const int PIXELS = 1920 * 1080;
        public const int PIXELS_YUV_420 = PIXELS + PIXELS >> 1;
        public fixed byte Data[PIXELS_YUV_420];
      
    }

    public readonly struct FrameInfo
    {
        public static readonly FrameInfo FullHD = new FrameInfo(1920, 1080, 1920);
        public static readonly FrameInfo SubHD = new FrameInfo(1456, 1088, 1456);
        public FrameInfo(int Width, int Height, int Stride)
        {
            this.Width = Width;
            this.Height = Height;
            this.Stride = Stride;
            Pixels = Width * Height;
            Yuv420 = Pixels + Pixels >> 1;
            Rows = Height + Height >> 1;
        }

        public int Pixels { get; }
        public int Yuv420 { get; }
        public int Width { get;  }
        public int Rows { get; }
        public int Height { get;  }
        public int Stride { get;  }

      
    }
   
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public unsafe struct SubHDFrame 
    {
        private const int PIXELS = 1456 * 1088;
        private const int PIXELS_YUV_420 = PIXELS + PIXELS >> 1;
        public fixed byte Data[PIXELS_YUV_420];
        
    }

   

    public interface IVideoStreamReplicator : IDisposable
    {
        event EventHandler Stopped;
        IMultiplexingStats MultiplexingStats { get; }
        DateTime Started { get; }
        VideoAddress VideoAddress { get; }
        IVideoStreamReplicator Connect();
        Task ReplicateTo(HttpContext ns, string? identifier, CancellationToken token = default);
        Task ReplicateTo(WebSocket ns, string? identifier);
        void ReplicateTo(Stream ns, string? identifier);
        bool Is(string name);
    }

    public class VideoStreamReplicator : IVideoStreamReplicator
    {
        public event EventHandler Stopped;
        private NetworkStream _source;
        private StreamMultiplexer? _multiplexer;
        private readonly string _host;
        private readonly string _protocol;
        private readonly HashSet<string> _tags = new();
        private readonly int _port;
        private readonly string _streamName;
        private readonly ILoggerFactory _loggerFactory;
        private readonly DateTime _started;
        private readonly IPlumber _plumber;
        private TcpClient _client;
        public IMultiplexingStats MultiplexingStats => _multiplexer;
        public DateTime Started => _started;
        
        public VideoAddress VideoAddress { get; private set; }
        public HashSet<string> Tags => _tags;
        public VideoStreamReplicator(VideoProtocol protocol, string host, int port, string streamName, string[] tags,
            ILoggerFactory loggerFactory, IPlumber plumber)
        {
            VideoAddress = new VideoAddress(protocol, host, port, streamName);
            _loggerFactory = loggerFactory;
            _plumber = plumber;
            
            _started = DateTime.Now;
            foreach (var tag in tags)
                _tags.Add(tag);
            
        }

        public IVideoStreamReplicator Connect()
        {
            if (_client != null) throw new InvalidOperationException("Cannot reuse Replicator.");

            _client = new TcpClient(_host, _port);
            try
            {
                _source = _client.GetStream();
                if (!string.IsNullOrWhiteSpace(_streamName))
                {
                    var bytes = Encoding.ASCII.GetBytes(_streamName);
                    _source.WriteByte((byte)bytes.Length);
                    _source.WriteAsync(bytes);
                }
                var streamingStarted = new StreamingStarted { Source = _host };
                
                _plumber.AppendEvent(streamingStarted, _host);
            }
            catch
            { 
                _client.Dispose();
                throw;
            }

            _multiplexer = new StreamMultiplexer(new NonBlockingNetworkStream(_source), _loggerFactory.CreateLogger<StreamMultiplexer>());
            _multiplexer.Stopped += OnStreamingStopped;
            _multiplexer.Disconnected += OnClientDisconnected;
            _multiplexer.Start();
            return this;
        }

        private void OnClientDisconnected(object? sender, EventArgs e)
        {
            var clientDisconnected = new ClientDisconnected
            {
                Source = _host
            };

            _plumber.AppendEvent(clientDisconnected, _host);
        }

        private void OnStreamingStopped(object? sender, EventArgs e)
        {
            Stopped?.Invoke(this, EventArgs.Empty);
            _client?.Dispose();
            _multiplexer!.Stopped -= OnStreamingStopped;
            _multiplexer.Disconnected -= OnClientDisconnected;
        }
        public async Task ReplicateTo(HttpContext ns, string? identifier, CancellationToken token = default)
        {
            var d = new ReverseMjpegDecoder();
            await _multiplexer!.Chase(ns, x => d.Decode(x) == JpegMarker.Start ? 0 : null, identifier, token);
        }
        public async Task ReplicateTo(WebSocket ns, string? identifier)
        {
            IDecoder d = new ReverseDecoder();
            await _multiplexer!.Chase(ns, x => d.Decode(x) == NALType.SPS ? 0 : null, identifier);
        }
        public void ReplicateTo(Stream ns, string? identifier)
        {
            if (this._protocol == "mjpeg")
            {
                var d = new ReverseMjpegDecoder();
                _multiplexer!.Chase(ns, x => d.Decode(x) == JpegMarker.Start ? 0 : null, identifier);
            }
            else
            {
                IDecoder d = new ReverseDecoder();
                _multiplexer!.Chase(ns, x => d.Decode(x) == NALType.SPS ? 0 : null, identifier);
            }
        }

        public bool Is(string name)
        {
            return VideoAddress.Host.Equals(name, StringComparison.CurrentCultureIgnoreCase) ||
                   string.Equals(VideoAddress.StreamName, name, StringComparison.CurrentCultureIgnoreCase) ||
                   Tags.Contains(name);
        }

        public void Dispose()
        {
            if(_multiplexer != null)
                _multiplexer.Disconnected -= OnClientDisconnected;
            _client?.Dispose();
        }
    }
    
}

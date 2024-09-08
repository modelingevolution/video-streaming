using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using EventPi.Abstractions;
using MicroPlumberd;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ModelingEvolution.VideoStreaming.Events;
using ModelingEvolution.VideoStreaming.Nal;


#pragma warning disable CS4014


namespace ModelingEvolution.VideoStreaming
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public unsafe struct FullHDFrame 
    {
        public const int PIXELS = 1920 * 1080;
        public const int PIXELS_YUV_420 = PIXELS + (PIXELS >> 1);
        public fixed byte Data[PIXELS_YUV_420];
      
    }

    public readonly struct FrameInfo
    {
        public static readonly FrameInfo FullHD = new FrameInfo(1920, 1080, 1920);
        public static readonly FrameInfo SubHD = new FrameInfo(1456, 1088, 1456);
        public static readonly FrameInfo HD = new FrameInfo(1280, 720, 1280);
        public static readonly FrameInfo Xga = new FrameInfo(1024, 768, 1024);
        public static readonly FrameInfo Svga = new FrameInfo(800, 600, 800);
        public static explicit operator FrameInfo(VideoResolution r)
        {
            switch (r)
            {
                case VideoResolution.FullHd: return FullHD;
                case VideoResolution.SubHd: return SubHD;
                case VideoResolution.Hd: return HD;
                case VideoResolution.Xga: return Xga;
                case VideoResolution.Svga: return Svga;
                default:
                    throw new ArgumentException("video resolution");
            }
        }
        public FrameInfo(int Width, int Height, int Stride)
        {
            this.Width = Width;
            this.Rows = this.Height = Height;
            this.Rows += Height >> 1;
            this.Stride = Stride;
            this.Yuv420 = Pixels = Width * Height;
            this.Yuv420 += Pixels >> 1;
            
        }

        public int Pixels { get; }
        public int Yuv420 { get; }
        public int Width { get;  }
        public int Rows { get; }
        public int Height { get;  }
        public int Stride { get;  }

        public override string ToString()
        {
            return $"{Width}x{Height}";
        }
    }
   
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public unsafe struct SubHDFrame 
    {
        private const int PIXELS = 1456 * 1088;
        private const int PIXELS_YUV_420 = PIXELS + (PIXELS >> 1);
        public fixed byte Data[PIXELS_YUV_420];
        
    }

   
    public class StoppedEventArgs(StoppedReason reason) : EventArgs
    {
        public StoppedReason Reason => reason;
    }
    public enum StoppedReason
    {
        ConnectionDisconnected,
        DeletedByUser
    }
    public interface IVideoStreamReplicator : IDisposable
    {
        event EventHandler<StoppedEventArgs> Stopped;
        IMultiplexingStats MultiplexingStats { get; }
        DateTime Started { get; }
        VideoAddress VideoAddress { get; }
        IVideoStreamReplicator Connect();
        Task ReplicateTo(HttpContext ns, string? identifier, CancellationToken token = default);
        Task ReplicateTo(WebSocket ns, string? identifier);
        void ReplicateTo(Stream ns, string? identifier, CancellationToken token = default);
        bool Is(string name);
        void Stop();
    }

    public class VideoStreamEventSink(IPlumber plumberd, IEnvironment e)
    {
        public async Task OnStreamingStarted(VideoAddress va)
        {
            var streamingStarted = new StreamingStarted { Source = va.ToString() };

            await plumberd.AppendEvent(streamingStarted, StreamingStarted.StreamId(e.HostName));
        }
        public async Task OnStreamingDisconnected(VideoAddress va)
        {
            var clientDisconnected = new ClientDisconnected
            {
                Source = va.ToString()
            };

            await plumberd.AppendEvent(clientDisconnected, ClientDisconnected.StreamId(e.HostName));
        }
    }
    public class VideoStreamReplicator : IVideoStreamReplicator
    {
        public event EventHandler<StoppedEventArgs> Stopped;
        private NetworkStream _source;
        private StreamMultiplexer? _multiplexer;
        
        private readonly string _protocol;
        
        
        private readonly string _streamName;
        private readonly ILoggerFactory _loggerFactory;
        private readonly DateTime _started;
        private readonly VideoStreamEventSink _evtSink;
        private CancellationTokenSource _cts;
        private TcpClient _client;
        public IMultiplexingStats MultiplexingStats => _multiplexer;
        public DateTime Started => _started;
        
        public VideoAddress VideoAddress { get; private set; }
        
        public VideoStreamReplicator(VideoAddress va,
            ILoggerFactory loggerFactory, VideoStreamEventSink sink)
        {
            VideoAddress = va;
            _loggerFactory = loggerFactory;
            _evtSink = sink;
            
            _started = DateTime.Now;
            
        }

        public IVideoStreamReplicator Connect()
        {
            if (_client != null) throw new InvalidOperationException("Cannot reuse Replicator.");

            _client = new TcpClient(VideoAddress.Host, VideoAddress.Port);
            try
            {
                _source = _client.GetStream();
                if (!string.IsNullOrWhiteSpace(_streamName))
                {
                    var bytes = Encoding.ASCII.GetBytes(_streamName);
                    _source.WriteByte((byte)bytes.Length);
                    _source.WriteAsync(bytes);
                }
                _evtSink.OnStreamingStarted(VideoAddress);
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
            _evtSink.OnStreamingDisconnected(VideoAddress);
        }

        private void OnStreamingStopped(object? sender, EventArgs e)
        {
            Stopped?.Invoke(this, new StoppedEventArgs(StoppedReason.ConnectionDisconnected));
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
        public void ReplicateTo(Stream ns, string? identifier, CancellationToken token = default)
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
                   VideoAddress.Tags.Contains(name);
        }

        public void Dispose()
        {
            if(_multiplexer != null)
                _multiplexer.Disconnected -= OnClientDisconnected;
            _client?.Dispose();
        }

        public void Stop()
        {
            _multiplexer?.Stop();
        }
    }
    
}

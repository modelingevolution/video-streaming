using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using EventPi.SharedMemory;
using MicroPlumberd;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ModelingEvolution.VideoStreaming.Chasers;
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
    public class VideoSharedBufferReplicator
    {
        private SharedCyclicBuffer? _buffer;
        private SharedBufferMultiplexer? _multiplexer;
        private FrameInfo _info;
        public string SharedMemoryName { get; private set; }

        public VideoSharedBufferReplicator(string sharedMemoryName, FrameInfo info)
        {
            SharedMemoryName = sharedMemoryName;
            _info = info;
        }

        public VideoSharedBufferReplicator Connect()
        {
            _buffer = new SharedCyclicBuffer(60, _info.Yuv420,  SharedMemoryName); // ~180MB
            _multiplexer = new SharedBufferMultiplexer(_buffer, _info);
            _multiplexer.Start();
            return this;
        }
    }

    public class SharedBufferMultiplexer : IMultiplexer
    {
        private Thread _worker;
        private readonly SharedCyclicBuffer _buffer;
        private readonly FrameInfo _info;

        public SharedBufferMultiplexer(SharedCyclicBuffer buffer, FrameInfo info)
        {
            _buffer = buffer;
            _info = info;
        }

        public void Start()
        {
            // Let's do it the old way, there's a semaphore. 
            _worker = new Thread(OnRun);
            _worker.IsBackground = true;
            _worker.Start();
        }

        private void OnRun()
        {
            while (true)
            {
                var ptr = _buffer.PopPtr();
                Mat m = new Mat(_info.Rows, _info.Width, MatType.CV_8U,ptr, _info.Stride);
                m.WriteToStream();
            }
        }

        public Memory<byte> Buffer()
        {
            
        }

        public int ReadOffset { get; }
        public ulong TotalReadBytes { get; }
        public void Disconnect(IChaser chaser)
        {
            
        }
    }

    public class VideoStreamReplicator : IDisposable
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
        public StreamMultiplexer StreamMultiplexer => _multiplexer;
        public string Host => _host;
        public string Protocol => _protocol;
        public DateTime Started => _started;
        public int Port => _port;
        public string StreamName => _streamName;
        public VideoAddress VideoAddress => new VideoAddress( this.Protocol,Host, Port, StreamName);
        public HashSet<string> Tags => _tags;
        public VideoStreamReplicator(string protocol, string host, int port, string streamName, string[] tags,
            ILoggerFactory loggerFactory, IPlumber plumber)
        {
            _host = host;
            this._port = port;
            _streamName = streamName;
            _loggerFactory = loggerFactory;
            _plumber = plumber;
            _protocol = protocol;
            _started = DateTime.Now;
            foreach (var tag in tags)
                _tags.Add(tag);
            
        }

        public VideoStreamReplicator Connect()
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

        public void Dispose()
        {
            if(_multiplexer != null)
                _multiplexer.Disconnected -= OnClientDisconnected;
            _client?.Dispose();
        }
    }
    
}

using System.Collections.Concurrent;
using System.Drawing;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using static System.Net.Mime.MediaTypeNames;

namespace ModelingEvolution.VideoStreaming.VectorGraphics;

public class RemoteCanvasStreamPool(ILoggerFactory loggerFactory)
{
    private record Item(ICanvas Canvas, BufferWriter Writer);
    private readonly ConcurrentDictionary<VideoAddress, Item> _items = new ConcurrentDictionary<VideoAddress, Item>();
    public ICanvas GetCanvas(VideoAddress va)
    {
        return Get(va).Canvas;
    }

    private Item Get(VideoAddress va)
    {
        return _items.GetOrAdd(va, va =>
        {
            var bufferWriter = new BufferWriter(loggerFactory);
            var item = new Item(new RemoteCanvas(bufferWriter.Begin, bufferWriter.Push, bufferWriter.End),
                bufferWriter);

            return item;
        });
    }
    public IWebSocketSink JoinWebSocket(VideoAddress va, WebSocket ws) => Get(va).Writer.Join(ws);
}

public interface IWebSocketSink : IDisposable
{
    Task WaitClose(CancellationToken token = default);
}

// Data can be written in chunks. We expect that a chunk cannot be bigger than _maxChunk. 
// We return Memory only that is required by a hint, no more. 
// A chunk of data needs to be in one-piece. If it happens to be that the chank is at the end of the 
// buffer and we want to write more data, than we will move it to the beginning of the buffer.
// The buffer acts as a cyclic chunk buffer. 
// When need chunk will be written, SlideChunk will be executed. Slide chunk acts as a if the SlidingBufferWriter looks like a new fresh buffer,
// however under the hood, it just takes next chunk.
public class BufferWriter(ILoggerFactory loggerFactory)
{
    readonly record struct Msg(MsgType Type, Memory<byte> Data);
    private readonly SlidingBufferWriter _buffer = new();
    
    private readonly BroadcastBlock<Msg> _data = new BroadcastBlock<Msg>(null);
    enum MsgType : byte
    {
        Start = 0x0, Obj= 0x1, End = 0x2,
    }
    

    class SinkBlock : IWebSocketSink
    {
        private readonly ActionBlock<Msg> _block;
        private readonly WebSocket _socket;
        private IDisposable? _link;
        private bool _started;
        private readonly ILogger<SinkBlock> _logger;
        private readonly AsyncManualResetEvent _closed = new AsyncManualResetEvent(false);
        public SinkBlock(WebSocket socket, ILogger<SinkBlock> logger)
        {
            _block = new ActionBlock<Msg>(OnReceive);
            this._socket = socket;
            _logger = logger;
            
        }

        public ITargetBlock<Msg> Block => _block;

        public IWebSocketSink LinkFrom(ISourceBlock<Msg> src)
        {
            _link = src.LinkTo(_block);
            return this;
        }
        private async Task OnReceive(Msg msg)
        {
            if (_socket.CloseStatus.HasValue)
            {
                _link?.Dispose();
                _closed.Set();
            }
            else
            {
                if (RequireNxMessage(msg)) return;
                try
                {
                    //_logger.LogInformation($"Sending: {msg.Type}, {msg.Data.Length}B");
                    await _socket.SendAsync(msg.Data, WebSocketMessageType.Binary, true, default);
                }
                catch(Exception ex)
                {
                    _logger.LogWarning(ex,"WebSocket Connection Closed");
                    _link.Dispose();
                    _link = null;
                    _closed.Set();
                }
            }
        }

        private bool RequireNxMessage(Msg msg)
        {
            if (_started) return false;
            if (msg.Type == MsgType.Start)
                _started = true;
            else return true;

            return false;
        }

        public void Dispose()
        {
            _socket.Dispose();
            _link?.Dispose();
            
        }

        public async Task WaitClose(CancellationToken token = default)
        {
            await this._closed.WaitAsync(token);
        }
    }
    public IWebSocketSink Join(WebSocket ws)
    {
        return new SinkBlock(ws, loggerFactory.CreateLogger<SinkBlock>()).LinkFrom(_data);
    }
    public void Push(IRenderOp obj)
    {
        _buffer.WriteFramePayload(obj.Id, obj);
        _data.Post(new Msg(MsgType.Obj,_buffer.WrittenMemory));
        _buffer.SlideChunk();
    }

    public void End()
    {
        _buffer.WriteFrameEnd();
        _data.Post(new Msg(MsgType.End, _buffer.WrittenMemory));
        _buffer.SlideChunk();
    }

    public void Begin(ulong obj)
    {
        _buffer.WriteFrameNumber(obj);
        _data.Post(new Msg(MsgType.Start, _buffer.WrittenMemory));
        _buffer.SlideChunk();
    }
}

public class RemoteCanvas(Action<ulong> onBegin, Action<IRenderOp> onPush, Action onEnd) : ICanvas
{
    
    public void DrawPolygon(IEnumerable<Vector> points, RgbColor? color = null)
    {
        var renderOp = new Draw<Polygon>
        {
            Value = new Polygon { Points = points is List<Vector> l ? l : points.ToList() },
            Context = new DrawContext
            {
             Stroke = color ?? RgbColor.Black
            }
        };
        onPush(renderOp);
    }

    public void End() => onEnd();
    public void Begin(ulong frameNr) => onBegin(frameNr);

    public void DrawText(string text, ushort x = 0, ushort y = 0, ushort size = 12, RgbColor? color = null)
    {
        var renderOp = new Draw<Text>
        {
            Value = new Text { Content = text },
            Context = new DrawContext
            {
                FontSize = size, 
                FontColor = color ?? RgbColor.Black, 
                Offset = new Vector { X = x, Y = y }
            }
        };
        onPush(renderOp);
    }

    
}
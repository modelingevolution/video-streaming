using System.Collections;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace ModelingEvolution.VideoStreaming.VectorGraphics;
// Uses websockets to communicate & .net protobuf serialization.
// Must be high performant, light-weight and native
// Frames consist of number and payload. The payload is an array of sub-frames. Each subframe consist uint type and ushort type of object to deserialize with protobuf. 
// At the end of the frame there is a marker Subframe with Size=0 and Type = ushort.Max.
// Deserialization is done just after a chunk of bytes is received. 
// After the EOF is received an event is raised.
public class ProtoStreamClient(ISerializer serializer, ILogger<ProtoStreamClient> logger) 
{
    public static readonly Frame Empty = new Frame(0,new List<object>(0));
    public readonly record struct Frame : IEnumerable<object>, IReadOnlyList<object>
    {
        public readonly ulong Number;
        internal readonly IList<object> Objects;
        public Frame(ulong nr)
        {
            this.Number = nr;
            this.Objects = new List<object>();
        }
        public Frame(ulong nr, IList<object> o)
        {
            this.Number = nr;
            this.Objects = o;
        }

        public int Count => Objects.Count;
        public IEnumerator<object> GetEnumerator()
        {
            return Objects.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)Objects).GetEnumerator();
        }

        public object this[int index] => Objects[index];
    }
    internal static readonly SubHeader EOF = new SubHeader(0, ushort.MaxValue);
    [StructLayout(LayoutKind.Explicit, Pack=1)]
    internal readonly record struct SubHeader
    {
        [FieldOffset(0)]
        public readonly uint Size;
            
        [FieldOffset(sizeof(uint))]
        public readonly ushort Type;

        public SubHeader()
        {
                
        }

        public SubHeader(uint size, ushort type)
        {
            this.Size = size;
            this.Type = type;
        }
    }

    private readonly Channel<Frame> _channel = Channel.CreateBounded<Frame>(new BoundedChannelOptions(1));
    private readonly ClientWebSocket _webSocket = new();
    private const int BufferSize = 8*1024*1024; // 8MB

    private Frame _currentFrame;

    private readonly CancellationTokenSource _cts = new();
        
    // Connect to the WebSocket server.
    public async Task ConnectAsync(Uri uri)
    {
        await _webSocket.ConnectAsync(uri, CancellationToken.None);
        logger.LogDebug("WebSocket connected!");
        _ = Task.Factory.StartNew(ReceiveLoop, TaskCreationOptions.LongRunning);
    }

    // This method constantly listens for incoming frames and processes them on the fly.
    private async Task ReceiveLoop()
    {
        try
        {
            Console.WriteLine("Receive loop...");
            var buffer = new byte[BufferSize];
            int offset = 0;
            while (_webSocket.State == WebSocketState.Open)
            {
                var bufferLength = buffer.Length - offset;
                var result =
                    await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, bufferLength),
                        CancellationToken.None);
                
                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    //Console.WriteLine("Received data: " + result.Count);
                    offset = ProcessFragment(buffer, result.Count);
                    
                    if (offset > 0)
                        Buffer.BlockCopy(buffer, result.Count - offset, buffer, 0, offset);
                } 
                //else 
                    //Console.WriteLine(result.MessageType);


                if (result.CloseStatus.HasValue)
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);

            }
            Console.WriteLine($"State is {_webSocket.State}");
        }
        catch(Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    // Close the WebSocket connection.
    public async Task DisconnectAsync()
    {
        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
    }

    private static readonly int SubHeaderSize = Marshal.SizeOf<SubHeader>();

    enum State
    {
        ReadingStart, ReadingSubFrames
    }

    private State _state = State.ReadingStart;
    // Process the incoming fragment of data, if data cannot be deserialized because it's missing, tail offset is returned.
    internal int ProcessFragment(ReadOnlySpan<byte> buffer, int length)
    {
        int offset = 0;
        // Ensure we have enough data to read the frame number
           
        while (offset < length)
        {
            if (_state == State.ReadingStart)
            {
                if (length - offset < sizeof(ulong))
                {
                    // Not enough data to read the frame number
                    return length - offset;
                }
                // Read the frame number
                ulong frameNumber = BitConverter.ToUInt64(buffer.Slice(offset));
                offset += sizeof(ulong);
                // Initialize the current frame if it is not already initialized or if the frame number has changed
                if (_currentFrame == Empty || _currentFrame.Number != frameNumber)
                    _currentFrame = new Frame(frameNumber);

                else throw new InvalidOperationException(("Duplicated frame detected!"));
                _state = State.ReadingSubFrames;
            } 
                
            // Read the sub-header
            if (length - offset < SubHeaderSize)
            {
                // Not enough data to read the sub-header
                return length - offset;
            }
            var subHeader = MemoryMarshal.Read<SubHeader>(buffer.Slice(offset));
            Console.WriteLine($"Deserialized: {subHeader}");
            
            offset += SubHeaderSize;
            if (subHeader.Equals(EOF))
            {
                // End of frame
                _channel.Writer.TryWrite(_currentFrame);
                _state = State.ReadingStart;
                continue;
            }
            if (length - offset < subHeader.Size)
            {
                // Not enough data to read the payload
                return length - offset + SubHeaderSize;
            }
            var payload = buffer.Slice(offset, (int)subHeader.Size);
            offset += (int)subHeader.Size;
            
            
            var deserializedObject = serializer.Deserialize(ref payload, subHeader.Type);
            _currentFrame.Objects.Add(deserializedObject);
        }
        return 0;
    }

    public IAsyncEnumerable<Frame> Read(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
using System;
using System.Buffers;
using System.Diagnostics;
using System.Drawing;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Channels;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Dai;
using EventPi.SharedMemory;
using MicroPlumberd;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ModelingEvolution.VideoStreaming.Buffers;
using ModelingEvolution.VideoStreaming.Chasers;
using ModelingEvolution.VideoStreaming.LibJpegTurbo;

namespace ModelingEvolution.VideoStreaming;
public class SharedBufferMultiplexer :  IShmMultiplexer
{
    public event EventHandler Disconnected;
    private Thread _worker;
    private readonly List<IChaser> _chasers = new();
    private readonly SharedCyclicBuffer _ipBuffer;
    private readonly FrameInfo _info;
    private readonly ILoggerFactory _loggerFactory;
    private readonly byte[] _sharedBuffer;
    private readonly Memory<byte> _buffer;
    private int _readOffset;
    private volatile int _lastFrameOffset;
    private readonly int _maxFrameSize;
    private readonly int _bufferSize;
    private volatile int _padding;
    private bool _isCanceled;
    private ulong _totalBytesRead;
    private ulong _readFrameCount;
    private readonly ILogger<SharedBufferMultiplexer> _logger;
    public ulong ReadFrameCount
    {
        get => _readFrameCount;
        private set => _readFrameCount = value;
    }
    public async IAsyncEnumerable<Frame> Read(int fps = 30, 
       [EnumeratorCancellation] CancellationToken token = default)
    {
        TimeSpan w = TimeSpan.FromSeconds(1d / (fps + fps));
        int offset = this.LastFrameOffset;
        var buffer = this.Buffer();
        while (this.ReadFrameCount == 0)
            await Task.Delay(w + w, token);

        ulong lastFrame = 0;
        
        while (!token.IsCancellationRequested)
        {
            if (this.IsEnd(offset))// buffer.Length - offset <= b.Padding)
            {
                offset = 0;
                Debug.WriteLine($"Buffer is full for reading, resetting.");
            }
            var metadata = buffer.ReadMetadata(offset);
            if (lastFrame == 0) lastFrame = metadata.FrameNumber;
            if (metadata.FrameNumber != lastFrame++)
            {
                // Frame is not in order. Resetting.
                var expecting = lastFrame - 1;
                var read = metadata.FrameNumber;
                var prvOffset = offset;
                offset = this.LastFrameOffset;
                metadata = buffer.ReadMetadata(offset);
                lastFrame = metadata.FrameNumber + 1;
                _logger?.LogWarning($"Frame not in order, resetting stream. Expecting: {expecting} " +
                                   $"received {read} from {prvOffset}, resetting to {metadata.FrameNumber} at {offset}");
            }

            if (!metadata.IsOk)
                throw new InvalidOperationException("Memory is corrupt or was overriden.");

            var pendingFrames = (int)(this.ReadFrameCount - metadata.FrameNumber);
            var pendingBytes = (int)(this.TotalReadBytes - metadata.StreamPosition);
            Frame f = new Frame(metadata,
                buffer.Slice(offset + METADATA_SIZE, (int)metadata.FrameSize),
                pendingFrames,
                pendingBytes);

            yield return f;

            offset += (int)metadata.FrameSize + METADATA_SIZE;
            while (metadata.FrameNumber == this.ReadFrameCount - 1)
                await Task.Delay(w, token); // might be spinwait?
        }
    }
    private static readonly int METADATA_SIZE = Marshal.SizeOf<FrameMetadata>();
    
    public IReadOnlyList<IChaser> Chasers => _chasers.AsReadOnly();
    public int Padding
    {
        get => _padding;
    }
    public int MaxFrameSize => _maxFrameSize;

    public bool IsCanceled
    {
        get => _isCanceled;
        set
        {
            _isCanceled = value;
            if (IsEnabled) IsEnabled = false;
        }
    }

    public bool IsRunning { get; private set; }
    public bool IsEnabled { get; private set; }
    public SharedBufferMultiplexer(SharedCyclicBuffer ipBuffer,
        FrameInfo info, ILoggerFactory loggerFactory)
    {
        _ipBuffer = ipBuffer;
        _info = info;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<SharedBufferMultiplexer>();
        _maxFrameSize = info.Yuv420;
        _bufferSize = _maxFrameSize * 30; // 1sec
        _sharedBuffer = ArrayPool<byte>.Shared.Rent(_bufferSize);
        _buffer = _sharedBuffer.AsMemory(0, _bufferSize);
        _logger.LogInformation($"Buffered prepared for: {info}");
    }

    public void Start()
    {
        // Let's do it the old way, there's a semaphore. 
        _worker = new Thread(OnRunHdr);
        _worker.IsBackground = true;
        _worker.Start();
        IsEnabled = true;
    }
    
    private unsafe void OnRunHdr()
    {
        IsRunning = true;
        var dstMemHandle = _buffer.Pin();
        var METADATA_SIZE = Marshal.SizeOf<FrameMetadata>();
        
        using var encoder = JpegEncoderFactory.Create(_info.Width, _info.Height, 80, 0);

        nint prv = IntPtr.Zero;
        byte[] mergeBuffer = new byte[_info.Width * _info.Height * 3/2];
        Memory<byte> mem = new Memory<byte>(mergeBuffer);
        var handle = mem.Pin();
        Stopwatch sw = new Stopwatch();
        ulong processingDurationTotal = 0;
        byte* mergePtr = (byte*)handle.Pointer;
        while (!IsCanceled)
        {
            try
            {
                // 1) Dispatching work
                // 2) Merging results

                //Mat m = new Mat(_info.Rows, _info.Width, MatType.CV_8U,ptr, _info.Stride);
                var left = _buffer.Length - _readOffset;
                if (left < (_maxFrameSize + METADATA_SIZE))
                {
                    _padding = left;
                    _readOffset = 0;
                    Debug.WriteLine($"Buffer is full for writing, resetting. Last frame was: {_readFrameCount - 1} ");
                    // We need to goto to begin of the buffer. There no more space left.
                }

                nint dstBuffer = (nint)dstMemHandle.Pointer;
                nint dstSlot = dstBuffer + _readOffset + METADATA_SIZE;

                var ptr = _ipBuffer.PopPtr();
                sw.Restart(); 

                ulong len = 0;
                if(prv != IntPtr.Zero)
                {
                    byte* src = (byte*)ptr;
                    byte* src2 = (byte*)prv;
                    for(int i = 0; i < mergeBuffer.Length; i++)
                    {                    
                        mergePtr[i] = (byte)((src[i] + src2[i])>>1);
                    }
                    len = encoder.Encode((nint)mergePtr, dstSlot, (ulong)_maxFrameSize);
                    prv = ptr;                    
                }
                else
                {
                    prv = ptr;
                    len = encoder.Encode(ptr, dstSlot, (ulong)_maxFrameSize);
                }

                
                if (len == 0)
                    throw new InvalidOperationException("Could not encode to jpeg");

                var m = new FrameMetadata(ReadFrameCount, len, _totalBytesRead);
                if (!m.IsOk)
                    throw new InvalidOperationException("Memory is corrupt or was overriden.");

                MemoryMarshal.Write(_buffer.Span.Slice(_readOffset), in m);
                _lastFrameOffset = _readOffset;
                _readOffset += (int)len + METADATA_SIZE;
                _totalBytesRead += len;
                Interlocked.Increment(ref _readFrameCount);
                processingDurationTotal += (ulong)sw.ElapsedMilliseconds;
                AvgPipelineExecution = (int)(processingDurationTotal / _readFrameCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encoding failed.");
                break;
            }
        }

        IsRunning = false;
    }
    public int AvgPipelineExecution { get; private set; }
    private unsafe void OnRun()
    {
        IsRunning = true;
        var dstMemHandle = _buffer.Pin();
        var METADATA_SIZE = Marshal.SizeOf<FrameMetadata>();
       
        using var encoder = JpegEncoderFactory.Create(_info.Width, _info.Height, 80, 0);
        while (!IsCanceled)
        {
            try
            {
               // 1) Dispatching work
               // 2) Merging results

                //Mat m = new Mat(_info.Rows, _info.Width, MatType.CV_8U,ptr, _info.Stride);
                var left = _buffer.Length - _readOffset;
                if (left < (_maxFrameSize + METADATA_SIZE))
                {
                    _padding = left;
                    _readOffset = 0;
                    Debug.WriteLine($"Buffer is full for writing, resetting. Last frame was: {_readFrameCount - 1} ");
                    // We need to goto to begin of the buffer. There no more space left.
                }

                nint dstBuffer = (nint)dstMemHandle.Pointer;
                nint dstSlot = dstBuffer + _readOffset + METADATA_SIZE;

                var ptr = _ipBuffer.PopPtr();


                var len = encoder.Encode(ptr, dstSlot, (ulong)_maxFrameSize);
                if (len == 0)
                    throw new InvalidOperationException("Could not encode to jpeg");

                var m = new FrameMetadata(ReadFrameCount, len, _totalBytesRead);
                if (!m.IsOk)
                    throw new InvalidOperationException("Memory is corrupt or was overriden.");

                MemoryMarshal.Write(_buffer.Span.Slice(_readOffset), in m);
                _lastFrameOffset = _readOffset;
                _readOffset += (int)len + METADATA_SIZE;
                _totalBytesRead += len;
                Interlocked.Increment(ref _readFrameCount);
            }
            catch(Exception ex) 
            {
                _logger.LogError(ex, "Encoding failed.");
                break;
            }
        }

        IsRunning = false;
    }

    public Memory<byte> Buffer() => _buffer;

    public bool IsEnd(int offset)
    {
        //buffer.Length - offset <= b.Padding
        var left = _buffer.Length - offset;
        return left <= _padding;
    }

    public int LastFrameOffset => _lastFrameOffset;

    public ulong TotalReadBytes => _totalBytesRead;
    public ulong BufferLength => _ipBuffer.Capacity + (ulong)_sharedBuffer.Length;

    public void Disconnect(IChaser chaser)
    {
        _chasers.Remove(chaser);
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public async Task Chase(HttpContext context, string identifier, CancellationToken token)
    {
        var chaser = new HttpMjpegBufferedFrameChaser(this, 
            context, 
            identifier, 
            _loggerFactory.CreateLogger<HttpMjpegBufferedFrameChaser>(),
            token: token);
        _chasers.Add(chaser);
        await chaser.Write();
    }

    public void Chase(Stream dst, string? identifier, CancellationToken token)
    {
        Tuple<Stream, string, IShmMultiplexer,CancellationToken> args =
            new Tuple<Stream, string, IShmMultiplexer,CancellationToken>(dst, identifier, this,token);

        StreamFrameChaser c = new StreamFrameChaser(dst, identifier,this, token);
        _chasers.Add(c);
        c.Start();
    }
}

public static class StreamFrameExtensions
{
    private static readonly int HEADER_SIZE = Marshal.SizeOf<FrameMetadata>();

    public static async Task StartCopyAsync(this Stream stream, Channel<JpegFrame> queue,
        int bufferSize = 16 * 1024 * 1024, bool throwOnEnd = true, CancellationToken token = default)
    {
        // Runs Copy in a separate long running thread.
        _ = Task.Factory.StartNew(async x =>
        {
            await stream.CopyAsync(queue, bufferSize, throwOnEnd, token);
        }, TaskContinuationOptions.LongRunning, token);
    }

    public static async Task CopyAsync(this Stream stream, Channel<JpegFrame> queue,
        int bufferSize = 16 * 1024 * 1024, bool throwOnEnd = true, CancellationToken token = default)
    {
        CyclicArrayBuffer b = new CyclicArrayBuffer(bufferSize);

        while (!token.IsCancellationRequested)
        {
            await stream.ReadIfRequired(b, HEADER_SIZE, throwOnEnd, token);
            // We have enough to read the header
            var m = MemoryMarshal.AsRef<FrameMetadata>(b.Use(HEADER_SIZE).Span);
            if (!m.IsOk)
                throw new InvalidOperationException("Memory is corrupt or was overriden.");

            await stream.ReadIfRequired(b, (int)m.FrameSize, throwOnEnd, token);
            var frame = b.Use((int)m.FrameSize);
            queue.Writer.TryWrite(new JpegFrame(m,frame));
        }
    }
}
public class StreamFrameChaser(Stream stream, string identifier, IShmMultiplexer buffer, CancellationToken token) : IChaser
{
    private DateTime _started;
    public int PendingBytes { get; private set; }

    public void Start()
    {
        _ = Task.Factory.StartNew(Run, TaskCreationOptions.LongRunning);
        _started = DateTime.Now;
    }

    public string Identifier => identifier;
    public ulong WrittenBytes { get; private set; }
    public string Started
    {
        get
        {
            var dur = DateTime.Now.Subtract(_started);
            return $"{_started:yyyy.MM.dd HH:mm} ({dur.ToString(@"dd\.hh\:mm\:ss")})";
        }
    }
    public async Task Close()
    {
        await stream.DisposeAsync();
    }

    async Task Run()
    {
        try
        {
            await foreach (var f in buffer.Read(token: token))
            {
                PendingBytes = f.PendingBytes;
                await stream.WriteAsync(f.Metadata);
                await stream.WriteAsync(f.Data, token);
                WrittenBytes += f.Metadata.FrameSize;
            }
        }
        catch (Exception ex)
        {
            buffer.Disconnect(this);
        }
    }
}
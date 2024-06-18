using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using EventPi.SharedMemory;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ModelingEvolution.VideoStreaming.Chasers;
using ModelingEvolution.VideoStreaming.LibJpegTurbo;

namespace ModelingEvolution.VideoStreaming;

#pragma warning disable CS4014
public class SharedBufferMultiplexer :  IBufferedFrameMultiplexer
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

    public ulong ReadFrameCount
    {
        get => _readFrameCount;
        private set => _readFrameCount = value;
    }

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
        _maxFrameSize = info.Yuv420;
        _bufferSize = _maxFrameSize * 30; // 1sec
        _sharedBuffer = ArrayPool<byte>.Shared.Rent(_bufferSize);
        _buffer = _sharedBuffer.AsMemory(0, _bufferSize);
    }

    public void Start()
    {
        // Let's do it the old way, there's a semaphore. 
        _worker = new Thread(OnRun);
        _worker.IsBackground = true;
        _worker.Start();
        IsEnabled = true;
    }

    private unsafe void OnRun()
    {
        IsRunning = true;
        var dstMemHandle = _buffer.Pin();
        var METADATA_SIZE = Marshal.SizeOf<FrameMetadata>();

        using var encoder = JpegEncoderFactory.Create(_info.Width, _info.Height, 80, 0);
        while (!IsCanceled)
        {
            var ptr = _ipBuffer.PopPtr();
            //Mat m = new Mat(_info.Rows, _info.Width, MatType.CV_8U,ptr, _info.Stride);
            var left = _buffer.Length - _readOffset;
            if (left < (_maxFrameSize+ METADATA_SIZE))
            {
                _padding = left;
                _readOffset = 0;
                Debug.WriteLine($"Buffer is full for writing, resetting. Last frame was: {_readFrameCount-1} ");
                // We need to goto to begin of the buffer. There no more space left.
            }
            nint dstBuffer = (nint)dstMemHandle.Pointer;
            nint dstSlot = dstBuffer + _readOffset + METADATA_SIZE;

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
}
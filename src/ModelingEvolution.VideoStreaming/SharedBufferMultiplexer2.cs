using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Emgu.CV;
using Emgu.CV.Util;
using EventPi.SharedMemory;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ModelingEvolution.VideoStreaming.Buffers;
using ModelingEvolution.VideoStreaming.Chasers;
using Emgu.CV.CvEnum;
using Emgu.CV.Shape;
using Tmds.Linux;
using System.Drawing;
using ModelingEvolution.VideoStreaming.Nal;
using System;

namespace ModelingEvolution.VideoStreaming;
public class CyclicMemoryBuffer
{
    private readonly uint _maxObjectSize;
    private readonly Memory<byte> _buffer;
    private readonly MemoryHandle _handle;
    private uint _cursor;
    private ulong _cycle;
    private ulong _written;
    public long Size => _buffer.Length;
    public ulong MaxObjectSize => _maxObjectSize;
    public CyclicMemoryBuffer(uint capacity, uint maxObjectSize)
    {
        _buffer = new Memory<byte>(new byte[capacity * maxObjectSize]);
        _handle = _buffer.Pin();
        _maxObjectSize = maxObjectSize;
    }
    public void Write<T>(in T data, uint offset = 0) where T : struct
    {
        MemoryMarshal.Write(_buffer.Span.Slice((int)(_cursor + offset)), in data);
    }
    public ulong SpaceLeft => (ulong)(_buffer.Length - _cursor);
    public unsafe byte* GetPtr(int offset = 0)
    {
        return ((byte*)_handle.Pointer) + _cursor + offset;
    }
    public Memory<byte> Use(uint size)
    {
        var u = _buffer.Slice((int)_cursor, (int)size);
        Advance(size);
        return u;
    }
    /// <summary>
    /// Advances the specified size.
    /// </summary>
    /// <param name="size">The size.</param>
    /// <returns>true if it is the next cycle in the buffer.</returns>
    public bool Advance(uint size)
    {
        bool nextIteration = false;
        var left = _buffer.Length - _cursor;
        if (left <= (_maxObjectSize))
        {
            _cursor = 0;
            _written += (ulong)size;
            nextIteration = true;
            Interlocked.Increment(ref _cycle);
            Debug.WriteLine($"Buffer is full for writing, resetting. Last frame was: {_cycle - 1} ");
        }
        else
        {
            _cursor += size;
            _written += (ulong)size;
        }
        return nextIteration;
    }
}
public class SharedBufferMultiplexer2 : IBufferedFrameMultiplexer
{
    
    public event EventHandler Disconnected;
    
    private readonly List<IChaser> _chasers = new();
    private readonly SharedCyclicBuffer _ipBuffer;
    private readonly FrameInfo _info;
    private readonly ILoggerFactory _loggerFactory;
    
    
    private int _readOffset;
    private volatile int _lastFrameOffset;
    private readonly int _maxFrameSize;
    private readonly int _bufferSize;
    private readonly VideoPipelineBuilder.VideoPipeline _pipeline;
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

    public IReadOnlyList<IChaser> Chasers => _chasers.AsReadOnly();
    public int Padding
    {
        get => _padding;
    }
    public int MaxFrameSize => _maxFrameSize;
    public async IAsyncEnumerable<Frame> Read(int fps = 30, [EnumeratorCancellation] CancellationToken token = default)
    {     
        foreach (var i in this._pipeline.Read(token))
            yield return new Frame(i.Value.Metadata, 
                i.Value.Data, 
                i.PendingItems, 
                (int)(_totalBytesRead - i.Value.Metadata.StreamPosition));
    }

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
    public SharedBufferMultiplexer2(SharedCyclicBuffer ipBuffer, 
        Func<YuvFrame, Nullable<YuvFrame>, ulong, int, PipeProcessingState, CancellationToken, JpegFrame> handler,
        FrameInfo info, ILoggerFactory loggerFactory)
    {
        
        _ipBuffer = ipBuffer;
        _info = info;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<SharedBufferMultiplexer>();
        _maxFrameSize = info.Yuv420;
        _bufferSize = _maxFrameSize * 30; // 1sec
        _pipeline = VideoPipelineBuilder.Create(info, OnGetItem, handler, loggerFactory);
        
        _logger.LogInformation($"Buffered prepared for: {info}");
    }
    ulong hdrProcessing;
    ulong encoding;
   
    
    public int AvgPipelineExecution => _pipeline.Pipeline.AvgPipeExecution;
    
    private unsafe JpegFrame OnProcessHdrSimple(YuvFrame frame,
       YuvFrame? prv,
       ulong secquence,
       int pipeId,
       PipeProcessingState state,
       CancellationToken token)
    {
        var sw = Stopwatch.StartNew();
        if (frame.Metadata.FrameNumber % 30 == 0)
        {
            _logger.LogInformation($"Avg pipe processing time: {_pipeline.Pipeline.AvgPipeExecution}ms");
            
        }

        var ptr = state.Buffer.GetPtr();

        var s = new Size(_info.Width, _info.Height);
        using Mat src = new Mat(s, DepthType.Cv8U, 3, (IntPtr)frame.Data, 0);
        
        IntPtr prvPtr = prv.HasValue ? (IntPtr)prv.Value.Data : (IntPtr)frame.Data;
        using Mat src2 = new Mat(s, DepthType.Cv8U, 3, prvPtr, 0);
                
        CvInvoke.AddWeighted(src, 0.5d, src2, 0.5d, 0, state.Dst, DepthType.Cv8U);

        var len = state.Encoder.Encode(state.Dst.DataPointer, (nint)ptr, state.Buffer.MaxObjectSize);
        var data = state.Buffer.Use((uint)len);

        var metadata = new FrameMetadata(frame.Metadata.FrameNumber, len, frame.Metadata.StreamPosition);
        return new JpegFrame(metadata, data);
    }
    CancellationTokenSource _cts;
    public void Start()
    {
        _cts = new CancellationTokenSource();
        _pipeline.Start(_cts.Token);
        IsEnabled = true;
        IsRunning = true;
    }
    ulong _fn = 0;
    ulong _stream = 0;
    private unsafe YuvFrame OnGetItem(CancellationToken token)
    {
        var ptr = _ipBuffer.PopPtr();
        var position = _totalBytesRead;
        _totalBytesRead += (ulong)_info.Yuv420;
        return new YuvFrame(new FrameMetadata(_fn++,(ulong)_info.Yuv420, position), _info, (byte*)ptr);
    }
   

    public ulong TotalReadBytes => _totalBytesRead;

    public ulong BufferLength => _pipeline.BufferSize;

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
        Tuple<Stream, string, IBufferedFrameMultiplexer, CancellationToken> args =
            new Tuple<Stream, string, IBufferedFrameMultiplexer, CancellationToken>(dst, identifier, this, token);

        StreamFrameChaser c = new StreamFrameChaser(dst, identifier, this, token);
        _chasers.Add(c);
        c.Start();
    }

    internal void Stop()
    {
        _pipeline.Pipeline.Stop();
        Disconnected?.Invoke(this, EventArgs.Empty);
    }
}
public static class StopWatchExtensions
{
   
    public static void MeasureReset(this Stopwatch stopwatch, ref ulong counter)
    {
        Interlocked.Add(ref counter, (ulong)stopwatch.ElapsedMilliseconds);
        stopwatch.Restart();
    }
}

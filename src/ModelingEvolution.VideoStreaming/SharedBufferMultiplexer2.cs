using System.Buffers;
using System.Collections.Concurrent;
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
using ModelingEvolution.VideoStreaming.LibJpegTurbo;
using Emgu.CV.CvEnum;
using Emgu.CV;
using Emgu.CV.Shape;
using Tmds.Linux;
using System.Drawing;

namespace ModelingEvolution.VideoStreaming;
#pragma warning disable CS4014
public readonly struct JpegFrame
{
    public readonly FrameMetadata Metadata;
    public readonly Memory<byte> Data;
    

    public JpegFrame(FrameMetadata metadata, Memory<byte> data)
    {
        Metadata = metadata;
        Data = data;
    }
}
public unsafe readonly struct YuvFrame
{
    public readonly byte* Data;
    public readonly FrameMetadata Metadata;

    public YuvFrame(FrameMetadata metadata, byte* data)
    {
        Data = data;
        Metadata = metadata;
    }
}

public class ConcurrentCyclicBuffer<T>
{
    private record Sloth(ManualResetEventSlim Ev) { public T Data { get; set; } }
    public record struct Item(T Value, int PendingItems, int Dropped);
    private readonly Sloth[] _index;
    private volatile int _cursor = -1;
    private long _written;
    private object _sync = new object();

    public ConcurrentCyclicBuffer(int count)
    {
        _index = new Sloth[count];
        for (int i = 0; i < count; i++)
            _index[i] = new Sloth(new ManualResetEventSlim(false, 50));
    }
    public long Written => _written;
    public int Count => _index.Length;
    public IEnumerable<Item> Read(CancellationToken token = default)
    {
        long c;
        int i;
        int dropped = 0;

        lock (_sync)
        {
            c = _written;
            i = Math.Max(0, _cursor);
        }

        for (; !token.IsCancellationRequested; i++)
        {
            var pending = _written - c++;
            i = i % _index.Length;
            if (pending >= _index.Length - 1)
            {
                i = _cursor;
                dropped += (int)pending;
            }

            _index[i].Ev.Wait(token);
            yield return new Item(_index[i].Data, (int)pending, dropped);
        }
    }
    public void Append(ref T data)
    {
        ManualResetEventSlim tmp = null;
        lock (_sync)
        {
            int index = (_cursor + 1) % _index.Length;
            int nx = (index + 1) % _index.Length;

            _index[nx].Ev.Reset();
            _index[index].Data = data;
            tmp = _index[index].Ev;
            _cursor = index;
            _written += 1;
        }
        tmp.Set();
    }
}
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
public class PipeProcessingState
{
    private int _quality = 80;
    
public JpegEncoder Encoder { get; }
    public MergeMertens MergeMertens = new MergeMertens(1, 0, 0);
    public CyclicMemoryBuffer Buffer { get; }
    public int Quality 
    { 
        get => _quality; 
        set {
            if(_quality == value) return;
            Encoder.Quality = value;
            _quality = value; 
        } 
    }
    public Mat Dst { get; }
    public PipeProcessingState(int w, int h, uint frameSize, uint count)
    {
        Encoder = JpegEncoderFactory.Create(w,h, Quality, 0);
        Buffer = new CyclicMemoryBuffer(count, frameSize);
        Dst = new Mat(new Size(w,h), DepthType.Cv8U, 3);
    }
}

public class VideoPipelineBuilder
{

    public class VideoPipeline
    {
        private readonly MultiPipeline<YuvFrame, PipeProcessingState, JpegFrame> _pipeline;
        private readonly FrameInfo _frameInfo;
        private readonly ConcurrentCyclicBuffer<JpegFrame> _buffer;
        private readonly uint _pipeBufferCount;
        public ulong BufferSize => (ulong)_pipeline.MaxParallelItems * _pipeBufferCount * (ulong)_frameInfo.Yuv420;
        private int _quality = 80;
        public int Quality 
        { 
            get => _quality;

        }
        public MultiPipeline<YuvFrame, PipeProcessingState, JpegFrame> Pipeline => _pipeline;
        public IEnumerable<ConcurrentCyclicBuffer<JpegFrame>.Item> Read(CancellationToken token = default) => _buffer.Read(token);
        public VideoPipeline(FrameInfo info, Func<CancellationToken, YuvFrame> getWorkItem,
            Func<YuvFrame, YuvFrame?, ulong, int, PipeProcessingState, CancellationToken, JpegFrame> onProcess, 
            uint pipeBufferCount, ILoggerFactory loggerFactory, int mergeBufferFrameCount, int maxParallelItems = 4)
        {
            this._frameInfo = info;
            this._buffer = new ConcurrentCyclicBuffer<JpegFrame>(mergeBufferFrameCount);
            this._pipeBufferCount = pipeBufferCount;
            this._pipeline = new MultiPipeline<YuvFrame, PipeProcessingState, JpegFrame>(maxParallelItems, OnCreatePipe, getWorkItem, onProcess, OnMerge, 
                loggerFactory.CreateLogger<MultiPipeline<YuvFrame, PipeProcessingState, JpegFrame>>());
        }

        private void OnMerge(JpegFrame frame, CancellationToken token)
        {
            _buffer.Append(ref frame);
        }

        private PipeProcessingState OnCreatePipe(int arg)
        {
            return new PipeProcessingState(_frameInfo.Width, _frameInfo.Height, (uint)_frameInfo.Yuv420, (uint)_pipeBufferCount);
        }

        public void Start(CancellationToken token = default)
        {
            _pipeline.Start(token);
            foreach (var i in _pipeline.Pipes)
                i.Quality = _quality;
        }
    }
    public static VideoPipeline Create(FrameInfo info, Func<CancellationToken, YuvFrame> getWorkItem,
        Func<YuvFrame, Nullable<YuvFrame>, ulong, int, PipeProcessingState, CancellationToken, JpegFrame> onProcess,
        ILoggerFactory loggerFactory,
        uint pipeBufferCount = 60 * 2, int mergeBufferFrameCount = 60 * 4)
    {
        return new VideoPipeline(info, getWorkItem, onProcess, pipeBufferCount, loggerFactory, mergeBufferFrameCount);
    }
}

public static class ThreadUtils
{
    [DllImport("libc")]
    private static extern int gettid();

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    public static uint GetThreadId()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetCurrentThreadId();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return (uint)gettid();

        } 
        else throw new NotSupportedException();
    }
}
public static class ThreadAffinity
{
    // Windows
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll")]
    private static extern IntPtr SetThreadAffinityMask(IntPtr hThread, IntPtr dwThreadAffinityMask);

    // Linux
    private const int CPU_SETSIZE = 1024;
    private const int __CPU_BITS = 8 * sizeof(ulong);
    [StructLayout(LayoutKind.Sequential)]
    public struct cpu_set_t
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = CPU_SETSIZE / __CPU_BITS)]
        public ulong[] _bits;

        public cpu_set_t()
        {
            _bits = new ulong[CPU_SETSIZE / __CPU_BITS];
        }

        public void Zero()
        {
            for (int i = 0; i < _bits.Length; i++)
                _bits[i] = 0;
        }

        public void Set(int cpu)
        {
            _bits[cpu / __CPU_BITS] |= 1UL << (cpu % __CPU_BITS);
        }
    }


    [DllImport("libc")]
    private static extern int sched_setaffinity(int pid, IntPtr cpusetsize, ref cpu_set_t mask);

    [DllImport("libc")]
    private static extern int sched_getaffinity(int pid, IntPtr cpusetsize, ref cpu_set_t mask);

    public unsafe static void SetAffinity(int processorIndex)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            IntPtr thread = GetCurrentThread();
            IntPtr mask = new IntPtr(1 << processorIndex);
            SetThreadAffinityMask(thread, mask);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var mask = new cpu_set_t();
            mask.Zero();
            mask.Set(processorIndex);
            int pid = 0; // 0 means current thread

            if (sched_setaffinity(pid, (IntPtr)sizeof(cpu_set_t), ref mask) != 0)
            {
                throw new InvalidOperationException("Error setting thread affinity");
            }
        }
        else
        {
            throw new PlatformNotSupportedException("This platform is not supported");
        }
    }
}

public sealed class MultiPipeline<TIn, TThreadState, TOut>(int maxParallelItems,
    Func<int, TThreadState> onCreatePipe,
    Func<CancellationToken, TIn> getWorkItem,
    Func<TIn, Nullable<TIn>, ulong, int, TThreadState, CancellationToken, TOut> process,
    Action<TOut, CancellationToken> mergeResults, 
    ILogger<MultiPipeline<TIn, TThreadState, TOut>> logger) : IDisposable
    where TIn : struct
{
    record StateData(TThreadState State, int Id);
    readonly record struct InData(ulong SeqNo, StateData Id, CancellationToken Token, TIn Data, TIn? Prv);
    readonly record struct OutData(ulong SeqNo, TOut Data);

    private SemaphoreSlim? _sem;
    private readonly ConcurrentBag<OutData> _results = new();
    private readonly ConcurrentQueue<StateData> _ids = new();
    private CancellationTokenSource _cts;
    private Thread? _dispatcher;
    private Thread? _merger;

    private CancellationToken _ct;
    private volatile int _running = 0;
    private ulong _dropped = 0;
    private ulong _outOfOrder = 0;
    private bool _isRunning = false;

    private ulong _dispatched = 0;
    private ulong _processed = 0;
    private ulong _merged = 0;
    private ulong _processingTimeMs;
    public int MaxParallelItems => maxParallelItems;
    public ulong Dropped => _dropped;
    public ulong OutOfOrder => _outOfOrder;
    public ulong InFlight => _dispatched - _merged;
    public ulong Finished => _merged;
    public bool IsRunning => _isRunning;
    public int AvgPipeProcessingTime => _processed == 0 ? int.MaxValue : (int)(_processingTimeMs / _processed);
    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;

        _cts.Cancel();

        _dispatcher.Join();
        while (_running > 0)
        {
            Thread.Yield();
        }
        _merger.Join();
        _cts.Dispose();
    }
    private TThreadState[] _pipes = Array.Empty<TThreadState>();
    public IEnumerable<TThreadState> Pipes => _pipes;
    public void Start(CancellationToken token = default)
    {
        if (_isRunning) throw new InvalidOperationException("Pipeline is already running");
        token.Register(() => _isRunning = false);

        _ids.Clear();
        _pipes = new TThreadState[maxParallelItems];
        for (int i = 0; i < maxParallelItems; i++)
        {
            _pipes[i] = onCreatePipe(i);
            _ids.Enqueue(new StateData(_pipes[i], i));
        }

        _isRunning = true;

        _sem?.Dispose();
        _sem = new SemaphoreSlim(0);
        _results.Clear();

        _dropped = _outOfOrder = _dispatched = _processed = _merged = 0;
        _running = 0;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        _ct = _cts.Token;
        _dispatcher = new Thread(OnDispatch);
        
        _dispatcher.IsBackground = true;
        _dispatcher.Start();

        _merger = new Thread(OnMergeResults);
        _merger.IsBackground = true;
        _merger.Start();
    }

    private void OnProcess(object state)
    {
        InData i = (InData)state;
        OnProcess(i.Data, i.Prv, i.Token, i.SeqNo, i.Id);

    }
    
    private void OnProcess(TIn data, TIn? prv, CancellationToken token, ulong seqNo, StateData id)
    {
        if (token.IsCancellationRequested)
        {
            _ids.Enqueue(id);
            Interlocked.Decrement(ref _running);
            return;
        }
        try
        {
            var sw = Stopwatch.StartNew();
            var ret = process(data, prv, seqNo, id.Id, id.State, token);
            Interlocked.Increment(ref _processed);
            Interlocked.Add(ref _processingTimeMs, (ulong)sw.ElapsedMilliseconds);

            _results.Add(new OutData(seqNo, ret));

            _ids.Enqueue(id);
            Interlocked.Decrement(ref _running);
            _sem.Release();
        }
        catch (OperationCanceledException)
        {
            _ids.Enqueue(id);
            Interlocked.Decrement(ref _running);
        }
    }

    private void OnMergeResults()
    {
        ThreadAffinity.SetAffinity(2);
        Thread.Yield();
        logger.LogInformation($"Merger at {ThreadUtils.GetThreadId()}");
        var buffer = new SortedList<ulong, TOut>(maxParallelItems);
        ulong cursor = 0;
        try
        {
            while (!_ct.IsCancellationRequested)
            {
                buffer.Clear();
                _sem.Wait(_ct);

                int c = _results.Count;
                uint oo = 0;
                for (int i = 0; i < c; i++)
                {
                    if (!_results.TryTake(out var t)) break;
                    if (t.SeqNo > cursor || cursor == 0)
                        buffer.Add(t.SeqNo, t.Data);
                    else
                    {
                        _outOfOrder += 1;
                        oo += 1;
                    }
                }

                _merged += oo; // because we threat skipped frames as merged.
                foreach (var item in buffer)
                {
                    mergeResults(item.Value, _ct);
                    _merged += 1;
                    cursor = item.Key;
                }

            }
        }
        catch (OperationCanceledException) { /* Do nothing */}
    }
    private void OnDispatch()
    {
        ThreadAffinity.SetAffinity(1);
        Thread.Yield();
        logger.LogInformation($"Dispatcher at {ThreadUtils.GetThreadId()}");
        ulong _i = 0;
        TIn? prv = default(TIn?);
        try
        {
            while (!_ct.IsCancellationRequested)
            {
                var item = getWorkItem(_ct);

                if (Interlocked.Increment(ref _running) > maxParallelItems)
                {
                    // we cannot process more items in parallel. We need to skip this one.
                    _dropped += 1;
                    Interlocked.Decrement(ref _running);
                    continue;
                }

                if (_ids.TryDequeue(out var r) && !ThreadPool.QueueUserWorkItem(OnProcess, new InData(_i++, r, _ct, item, prv)))
                    throw new InvalidOperationException("Cannot enqueue process operation.");

                prv = item;
                _dispatched += 1;
            }
        }
        catch (OperationCanceledException) { return; }
    }

    public void Dispose()
    {
        Stop();
        _sem.Dispose();
        _cts.Dispose();
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
        FrameInfo info, ILoggerFactory loggerFactory)
    {
        
        _ipBuffer = ipBuffer;
        _info = info;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<SharedBufferMultiplexer>();
        _maxFrameSize = info.Yuv420;
        _bufferSize = _maxFrameSize * 30; // 1sec
        _pipeline = VideoPipelineBuilder.Create(info, OnGetItem, OnProcessHdrSimple, loggerFactory);
        
        _logger.LogInformation($"Buffered prepared for: {info}");
    }
    ulong hdrProcessing;
    ulong encoding;
    ulong convertion;
    ulong allocation;
    ulong processed;
    private unsafe JpegFrame OnProcessHdr(YuvFrame frame,
        YuvFrame? prv,
        ulong secquence,
        int pipeId,
        PipeProcessingState state,
        CancellationToken token)
    {
        var sw = Stopwatch.StartNew();
        if(frame.Metadata.StreamPosition % 30 == 0)
        {
            _logger.LogInformation($"Avg pipe processing time: {_pipeline.Pipeline.AvgPipeProcessingTime}ms");
            if(processed != 0)
                _logger.LogInformation($"Hdr: {hdrProcessing / processed}, allocation: {allocation / processed}, convertion: {convertion / processed}");
        }
        var ptr = state.Buffer.GetPtr();
                
        var s = new Size(_info.Width, _info.Height);
        using Mat src = new Mat(s, DepthType.Cv8U, 3, (IntPtr)frame.Data, 0);

        IntPtr prvPtr = prv.HasValue ? (IntPtr)prv.Value.Data : (IntPtr)frame.Data;
        using Mat src2 = new Mat(s, DepthType.Cv8U, 3, prvPtr, 0);
        using Mat hdrFrame = new Mat();
        using Mat dst = new Mat();

        using var tmp = new VectorOfMat(src, src2);
        sw.MeasureReset(ref allocation);

        state.MergeMertens.Process(tmp, hdrFrame);
        sw.MeasureReset(ref hdrProcessing);

        hdrFrame.ConvertTo(dst, DepthType.Cv8U, 255.0);

        var len = state.Encoder.Encode(dst.DataPointer, (nint)ptr, state.Buffer.MaxObjectSize);
        var data = state.Buffer.Use((uint)len);
        sw.MeasureReset(ref convertion);
        Interlocked.Increment(ref processed);
        var metadata = new FrameMetadata(frame.Metadata.FrameNumber, len, frame.Metadata.StreamPosition);
        return new JpegFrame(metadata, data);
    }
    private unsafe JpegFrame OnProcess(YuvFrame frame, 
        YuvFrame? prv, 
        ulong secquence, 
        int pipeId, 
        PipeProcessingState state, 
        CancellationToken token)
    {
        var ptr= state.Buffer.GetPtr();

        var len = state.Encoder.Encode((nint)frame.Data, (nint)ptr, state.Buffer.MaxObjectSize);
        var data = state.Buffer.Use((uint)len);
        var metadata = new FrameMetadata(frame.Metadata.FrameNumber, len, frame.Metadata.StreamPosition);
        return new JpegFrame(metadata, data);
    }
    private unsafe JpegFrame OnProcessHdrSimple(YuvFrame frame,
       YuvFrame? prv,
       ulong secquence,
       int pipeId,
       PipeProcessingState state,
       CancellationToken token)
    {
        var sw = Stopwatch.StartNew();
        if (frame.Metadata.StreamPosition % 30 == 0)
        {
            _logger.LogInformation($"Avg pipe processing time: {_pipeline.Pipeline.AvgPipeProcessingTime}ms");
            if (processed != 0)
                _logger.LogInformation($"Hdr: {hdrProcessing / processed}, allocation: {allocation / processed}, convertion: {convertion / processed}");
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

    public void Start()
    {
        _pipeline.Start();
        IsEnabled = true;
        IsRunning = true;
    }
    private unsafe YuvFrame OnGetItem(CancellationToken token)
    {
        var ptr = _ipBuffer.PopPtr();
        _totalBytesRead += (ulong)_info.Yuv420;
        return new YuvFrame(new FrameMetadata(), (byte*)ptr);
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
}
public static class StopWatchExtensions
{
   
    public static void MeasureReset(this Stopwatch stopwatch, ref ulong counter)
    {
        Interlocked.Add(ref counter, (ulong)stopwatch.ElapsedMilliseconds);
        stopwatch.Restart();
    }
}

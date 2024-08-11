using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ModelingEvolution.VideoStreaming.LibJpegTurbo;

namespace ModelingEvolution.VideoStreaming;

public sealed class MultiPipeline<TIn, TThreadState, TOut>(int maxParallelItems,
    Func<int, TThreadState> onCreatePipe,
    Func<CancellationToken, TIn> getWorkItem,
    Func<TIn, Nullable<TIn>, ulong, int, TThreadState, CancellationToken, TOut> process,
    Action<TOut, CancellationToken> mergeResults, 
    ILogger<MultiPipeline<TIn, TThreadState, TOut>> logger) : IDisposable
    where TIn : struct
{
    readonly record struct PartialProcessInput(PartialProcessing Process, InData Data);
    record PartialProcessing(int Every, object State, Action<TIn, Nullable<TIn>, ulong, CancellationToken, object> Action);

    record StateData(TThreadState State, int Id);
    readonly record struct InData(ulong SeqNo, StateData Id, CancellationToken Token, TIn Data, TIn? Prv);
    readonly record struct OutData(ulong SeqNo, TOut Data);

    private readonly List<PartialProcessing> _partialProcessing = new();

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
    public int AvgPipeExecution => _processed == 0 ? int.MaxValue : (int)(_processingTimeMs / _processed);
    public void SubscribePartialProcessing(Action<TIn, Nullable<TIn>, ulong, CancellationToken, object> action,object state, int every)
    {
        _partialProcessing.Add(new PartialProcessing(every, state, action));
    }
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

    private void OnProcess(object? state)
    {
        InData i = (InData)state!;
        OnProcess(i.Data, i.Prv, i.Token, i.SeqNo, i.Id);

    }
    private void OnProcessPartial(object? state)
    {
        var d = (PartialProcessInput)state!;
        var i = d.Data;
        d.Process.Action(i.Data, i.Prv, i.SeqNo, i.Token, d.Process.State);

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
        catch (OperationCanceledException) {
            /* Do nothing */
            Debug.WriteLine("OnMergeResults canceled.");
        }
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

                if (_ids.TryDequeue(out var r))
                {
                    var data = new InData(_i++, r, _ct, item, prv);
                    if (!ThreadPool.QueueUserWorkItem(OnProcess, data))
                        throw new InvalidOperationException("Cannot enqueue process operation.");


                    // Not sure if this should be here
                    foreach (var i in _partialProcessing)
                    {
                        if (((long)(_i) % i.Every) == 0)
                            ThreadPool.QueueUserWorkItem(OnProcessPartial, new PartialProcessInput(i, data));

                    }
                }
                prv = item;
                _dispatched += 1;
            }
        }
       
        catch (ObjectDisposedException) { return; }
        catch (InvalidOperationException)
        {
            if (Debugger.IsAttached){
                Debug.WriteLine("Normally app would shutdown. But because you're debugging we let you continue. The dispatch loop has exited.");
                return;
            } else throw;
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("OnDispatch canceled.");
            return;
        }
    }

    public void Dispose()
    {
        Stop();
        _sem.Dispose();
        _cts.Dispose();
    }
}

using System.Collections.Concurrent;

namespace ModelingEvolution.VideoStreaming;

sealed class WorkPipeline<TIn, TOut>(int maxParallelItems,
        Func<CancellationToken, TIn> getWorkItem,
        Func<TIn, ulong, int, CancellationToken, TOut> process,
        Action<TOut, CancellationToken> mergeResults) : IDisposable
{
    readonly record struct InData(ulong SeqNo, int Id, CancellationToken Token, TIn Data);
    readonly record struct OutData(ulong SeqNo, TOut Data);

    private readonly ManualResetEventSlim _resultEvent = new ManualResetEventSlim(false);
    private readonly ConcurrentBag<OutData> _results = new();
    private readonly ConcurrentQueue<int> _ids = new();
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
    public ulong Dropped => _dropped;
    public ulong OutOfOrder => _outOfOrder;
    public ulong InFlight => _dispatched - _merged;
    public ulong Finished => _merged;
    public bool IsRunning => _isRunning;

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
    public void Start(CancellationToken token = default)
    {
        if (_isRunning) throw new InvalidOperationException("Pipeline is already running");
        token.Register(() => _isRunning = false);

        _ids.Clear();
        for (int i = 0; i < maxParallelItems; i++)
            _ids.Enqueue(i);

        _isRunning = true;

        _results.Clear();
        _resultEvent.Reset();
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
        OnProcess(i.Data, i.Token, i.SeqNo, i.Id);

    }
    private void OnProcess(TIn data, CancellationToken token, ulong seqNo, int id)
    {
        if (token.IsCancellationRequested)
        {
            _ids.Enqueue(id);
            Interlocked.Decrement(ref _running);
            return;
        }
        try
        {
            var ret = process(data, seqNo, id, token);
            Interlocked.Increment(ref _processed);

            _results.Add(new OutData(seqNo, ret));

            _ids.Enqueue(id);
            Interlocked.Decrement(ref _running);
            _resultEvent.Set();
        }
        catch (OperationCanceledException)
        {
            _ids.Enqueue(id);
            Interlocked.Decrement(ref _running);
        }
    }

    private void OnMergeResults()
    {
        var buffer = new SortedList<ulong, TOut>(maxParallelItems);
        ulong cursor = 0;
        try
        {
            while (!_ct.IsCancellationRequested)
            {
                buffer.Clear();
                _resultEvent.Wait(_ct);
                _resultEvent.Reset();

                int c = _results.Count;
                uint oo = 0;
                for (int i = 0; i < c; i++)
                {
                    if (!_results.TryTake(out var t)) continue;
                    if (t.SeqNo > cursor || cursor == 0)
                        buffer.Add(t.SeqNo, t.Data);
                    else
                    {
                        _outOfOrder += 1;
                        oo += 1;
                    }
                }

                _merged += oo;
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
        ulong _i = 0;
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

                if (_ids.TryDequeue(out int r) && !ThreadPool.QueueUserWorkItem(OnProcess, new InData(_i++, r, _ct, item)))
                    throw new InvalidOperationException("Cannot enqueue process operation.");

                _dispatched += 1;
            }
        }
        catch (OperationCanceledException) { return; }
    }

    public void Dispose()
    {
        Stop();
        _resultEvent.Dispose();
        _cts.Dispose();
    }
}

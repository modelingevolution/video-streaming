namespace ModelingEvolution.VideoStreaming;

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

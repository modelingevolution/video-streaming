using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ModelingEvolution.VideoStreaming.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace ModelingEvolution.VideoStreaming.Chasers;

internal sealed class Chaser : IChaser
{
    private readonly Stream _dst;
    private readonly Func<byte, int?> _validStart;
    private readonly CancellationTokenSource _cancellationTokenSource;

    private readonly IStreamMultiplexer _multiplexer;
    private ulong _written;
    private readonly DateTime _started;
    public bool IsMultiplexer { get; }
    public int PendingBytes => _pendingWrite;
    public ulong WrittenBytes => _written;
    private int _pendingWrite = 0;
    public string Identifier { get; }
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
        _cancellationTokenSource.Cancel();
        await _dst.DisposeAsync();
    }

    public Chaser(IStreamMultiplexer multiplexer, Stream dst, Func<byte, int?> validStart, string identifier = null)
    {
        _dst = dst;
        _validStart = validStart;
        _multiplexer = multiplexer;
        _written = 0;
        _cancellationTokenSource = new CancellationTokenSource();
        _started = DateTime.Now;
        Identifier = identifier;
    }
    private async Task OnWrite()
    {
        try
        {
            //TestStream();
            await OnWriteAsync(_dst, _validStart);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Chaser failed, disconnecting. \n\tPending bytes: {_pendingWrite}B \n\tWritten bytes: {_written}B \n\nError message: {ex.Message}\nStack trace:\n{ex.StackTrace}");
            for (Exception? i = ex.InnerException; i != null; i = i.InnerException)
                Console.WriteLine($"Inner Exception: {i}");

        }
        _dst.Close();
        _multiplexer.Disconnect(this);
    }



    private void FindStartOffset(ref int offset, ref int count, Func<byte, int?> validStart)
    {
        var span = _multiplexer.Buffer().Span;
        int nc = 0;
        for (int i = offset - 1; nc < count + 1; i--)
        {
            nc += 1;
            if (i < 0) i = span.Length - 1;

            var ch = validStart(span[i]);
            if (ch != null)
            {
                offset = i + ch.Value;
                count = nc;
                return;
            }
        }

        throw new InvalidOperationException("Could not find valid start :(");
    }



    public void Cancel()
    {
        _cancellationTokenSource.Cancel();
    }

    private async Task OnWriteAsync(Stream destination, Func<byte, int?> validStart)
    {
        var offset = _multiplexer.ReadOffset;
        var currentTotal = _multiplexer.TotalReadBytes;
        var count = (int)Math.Min(currentTotal, (ulong)_multiplexer.Buffer().Length);


        FindStartOffset(ref offset, ref count, validStart);
        // count is pending bytes. Offset is whenever.

        // this might big number,
        ulong started = currentTotal - (ulong)count;

        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            _pendingWrite = (int)(_multiplexer.TotalReadBytes - started - _written);
            if (_pendingWrite > _multiplexer.Buffer().Length)
                throw new ChaserWriteFailedException("There is more pending bytes than buffer size.");

            if (_pendingWrite > 0)
            {
                int inlineLeft = _multiplexer.Buffer().Length - offset;
                if (inlineLeft == 0)
                {
                    offset = 0;
                    inlineLeft = _multiplexer.Buffer().Length;
                }

                count = Math.Min(inlineLeft, _pendingWrite);

                var slice = _multiplexer.Buffer().Slice(offset, count);
                if (!destination.CanWrite)
                    throw new ChaserWriteFailedException("Stream is not writable.");
                try
                {
                    await destination.WriteAsync(slice);
                }
                catch (Exception ex)
                {
                    throw new ChaserWriteFailedException(ex, count, _started);
                }

                _written += (ulong)count;
                offset += count;
            }
            else
            {
                await Task.Delay(1000 / 60);
            }
        }
    }

    public void Start()
    {
        Task.Factory.StartNew(OnWrite, TaskCreationOptions.LongRunning);
    }
}
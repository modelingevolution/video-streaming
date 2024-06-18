using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace ModelingEvolution.VideoStreaming.Chasers;

internal sealed class HttpMjpegStreamChaser : IChaser
{
    private readonly HttpContext _dst;
    private readonly Func<byte, int?> _validStart;
    private readonly CancellationTokenSource _cancellationTokenSource;

    private readonly IStreamMultiplexer _multiplexer;
    private ulong _written;
    private readonly DateTime _started;
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
        await _dst.Response.Body.DisposeAsync();
    }
    private static readonly string UuidBoundary = Guid.NewGuid().ToString().ToLower().Replace("-", "");
    public static readonly byte[] Boundary = Encoding.UTF8.GetBytes($"\r\n--{UuidBoundary}\r\nContent-Type: image/jpeg\r\n\r\n");
    public static readonly byte[] StartBoundary = Encoding.UTF8.GetBytes($"--{UuidBoundary}\r\nContent-Type: image/jpeg\r\n\r\n");
    public static readonly byte[] EndBoundary = Encoding.UTF8.GetBytes($"\r\n--{UuidBoundary}--");

    public static string BoundaryHeader => $"{UuidBoundary}";
    private readonly MjpegDecoder _stateDetector = new MjpegDecoder();
    private long _counter = 0;
    private long _frameCounter = 0;
    private readonly long _maxFrames;


    private async Task<bool> WriteTo(ReadOnlyMemory<byte> data, Stream writer)
    {
        int start = 0;
        for (int i = 0; i < data.Length; i++)
        {
            if (_stateDetector.Decode(data.Span[i]) != JpegMarker.End) continue;

            var rm = data.Slice(start, i - start + 1);
            await writer.WriteAsync(rm);

            _frameCounter += 1;

            if (_frameCounter > _maxFrames)
            {
                await writer.WriteAsync(EndBoundary);
                return false;
            }
            await writer.WriteAsync(Boundary);
            await writer.FlushAsync();
            start = i + 1;
            _counter += rm.Length + Boundary.LongLength;

        }

        var count = data.Length - start;
        if (count > 0)
        {
            var rm = data.Slice(start, count);
            await writer.WriteAsync(rm);
            _counter += rm.Length;
        }

        return true;
    }
    public HttpMjpegStreamChaser(IStreamMultiplexer multiplexer, HttpContext dst, Func<byte, int?> validStart,
        string identifier = null, long? maxFrames = null, CancellationToken token = default)
    {
        _dst = dst;
        _validStart = validStart;
        _multiplexer = multiplexer;
        _written = 0;

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);

        _started = DateTime.Now;

        _maxFrames = maxFrames ?? long.MaxValue;
        Identifier = identifier;
    }
    private async Task OnWrite()
    {
        try
        {
            //TestStream();
            await OnWriteAsync(_dst, _validStart, _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Chaser failed, disconnecting. \n\tPending bytes: {_pendingWrite}B \n\tWritten bytes: {_written}B \n\nError message: {ex.Message}\nStack trace:\n{ex.StackTrace}");
            for (Exception? i = ex.InnerException; i != null; i = i.InnerException)
                Console.WriteLine($"Inner Exception: {i}");

        }
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

    private async Task OnWriteAsync(HttpContext cx, Func<byte, int?> validStart, CancellationToken cancellationToken)
    {
        cx.Response.StatusCode = (int)HttpStatusCode.OK;
        cx.Response.ContentType = $"multipart/x-mixed-replace; boundary={BoundaryHeader}";
        //cx.Response.Headers.CacheControl = "no-cache";


        var offset = _multiplexer.ReadOffset;
        var currentTotal = _multiplexer.TotalReadBytes;
        var count = (int)Math.Min(currentTotal, (ulong)_multiplexer.Buffer().Length);


        FindStartOffset(ref offset, ref count, validStart);
        // count is pending bytes. Offset is whenever.

        // this might big number,
        ulong started = currentTotal - (ulong)count;
        await cx.Response.Body.WriteAsync(StartBoundary, 0, StartBoundary.Length, cancellationToken);
        await cx.Response.Body.FlushAsync(cancellationToken);
        var writer = cx.Response.Body;

        while (!cancellationToken.IsCancellationRequested)
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

                try
                {

                    if (!await WriteTo(slice, writer))
                        break;
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

    public Task Write()
    {
        return OnWrite();
    }
    public void Start()
    {
        Task.Factory.StartNew(OnWrite, TaskCreationOptions.LongRunning);
    }
}
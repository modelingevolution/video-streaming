using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ModelingEvolution.VideoStreaming.Chasers;

internal sealed class HttpMjpegBufferedFrameChaser : IChaser
{
    private readonly HttpContext _dst;
    private readonly ILogger<HttpMjpegBufferedFrameChaser> _logger;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly IBufferedFrameMultiplexer _multiplexer;
    private ulong _frameCounter = 0;
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

    public static readonly byte[] Boundary =
        Encoding.UTF8.GetBytes($"\r\n--{UuidBoundary}\r\nContent-Type: image/jpeg\r\n\r\n");

    public static readonly byte[] StartBoundary =
        Encoding.UTF8.GetBytes($"--{UuidBoundary}\r\nContent-Type: image/jpeg\r\n\r\n");

    public static readonly byte[] EndBoundary = Encoding.UTF8.GetBytes($"\r\n--{UuidBoundary}--");

    public static string BoundaryHeader => $"{UuidBoundary}";
    
    

    
    public HttpMjpegBufferedFrameChaser(IBufferedFrameMultiplexer multiplexer,
        HttpContext dst,
        string identifier = null,
        ILogger<HttpMjpegBufferedFrameChaser> logger = null,
        long? maxFrames = null,
        CancellationToken token = default)
    {
        _dst = dst;
        _logger = logger;
        _multiplexer = multiplexer;
        _written = 0;
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);

        _started = DateTime.Now;
        Identifier = identifier;
    }

    private async Task OnWrite()
    {
        try
        {
            //TestStream();
            await OnWriteAsync(_dst,  _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Chaser failed, disconnecting. \n\tPending bytes: {_pendingWrite}B \n\tWritten bytes: {_written}B \n\nError message: {ex.Message}\nStack trace:\n{ex.StackTrace}");
            for (Exception? i = ex.InnerException; i != null; i = i.InnerException)
                Console.WriteLine($"Inner Exception: {i}");

        }

        _multiplexer.Disconnect(this);
    }

    public void Cancel()
    {
        _cancellationTokenSource.Cancel();
    }

    private async Task OnWriteAsync(HttpContext cx, CancellationToken cancellationToken)
    {
        cx.Response.StatusCode = (int)HttpStatusCode.OK;
        cx.Response.ContentType = $"multipart/x-mixed-replace; boundary={BoundaryHeader}";
        //cx.Response.Headers.CacheControl = "no-cache";
        
        await cx.Response.Body.WriteAsync(StartBoundary, 0, StartBoundary.Length, cancellationToken);
        await cx.Response.Body.FlushAsync(cancellationToken);
        var writer = cx.Response.Body;
        int c = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            await foreach(var data in _multiplexer.Read(token: cancellationToken))
            {
                
                await writer.WriteAsync(data.Data, cancellationToken);
                if (!MjpegDecoder.IsJpeg(data.Data))
                    throw new InvalidOperationException("Frame is not valid jpeg");
                await writer.WriteAsync(Boundary, cancellationToken);
                
                _frameCounter += 1;
                _written += (ulong)data.Data.Length + (ulong)Boundary.Length;
                _pendingWrite = data.PendingBytes;
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
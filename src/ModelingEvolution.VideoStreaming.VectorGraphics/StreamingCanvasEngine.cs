using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace ModelingEvolution.VideoStreaming.VectorGraphics;

public class StreamingCanvasEngine
{
    private readonly ProtoStreamClient _client;
    private readonly SkiaCanvas _canvas;
    private CancellationTokenSource? _cts;
    public StreamingCanvasEngine(ILoggerFactory factory)
    {
        var r = MessageRegisterBuilder.Create()
            .With(1, typeof(Draw<Text>)).Build();
        
        _client = new ProtoStreamClient(new Serializer(r), factory.CreateLogger<ProtoStreamClient>());
        _canvas = new SkiaCanvas();
    }
    public async Task ConnectAsync(Uri uri)
    {
        if (IsRunning) throw new InvalidOperationException("Engine is already running");
        Uri = uri;
        await _client.ConnectAsync(uri);
        _cts = new CancellationTokenSource();
        _ = Task.Factory.StartNew(OnStartStreaming, TaskCreationOptions.LongRunning);
    }
    public Uri Uri { get; private set; }
    public ulong Frame { get; private set; }
    public float Fps { get; private set; }
    public bool IsRunning { get; private set; }
    public string? Error { get; private set; }
    private async Task OnStartStreaming()
    {
        try
        {
            IsRunning = true;
            Error = null;
            FpsWatch sw = new();

            await foreach (var i in _client.Read(_cts!.Token))
            {
                Frame = i.Number;
                Fps = (float)sw++.Value;
                foreach (var o in i.OfType<IRenderOp>())
                    _canvas.Add(o);

                _canvas.Complete();
            }

        }
        catch (Exception ex)
        {
            Error = ex.Message;
            
        }
        IsRunning = false;
        _cts = null;
    }

    public async Task StopAsync()
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
            await _client.DisconnectAsync();
        }
    }
    public void Render(SKCanvas canvas)
    {
        canvas.Clear();
        _canvas.Render(canvas);
    }
}

public class FpsWatch
{
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    public TimeSpan MeasurePeriod = TimeSpan.FromSeconds(5);
    private ulong _i = 0;
    private double _lastFps;
    public double Value => _lastFps;
    public static explicit operator double(FpsWatch watch)
    {
        return watch._lastFps;
    }
    public static FpsWatch operator ++(FpsWatch watch)
    {
        watch._i++;
        if (watch._sw.Elapsed <= watch.MeasurePeriod) return watch;
        
        watch._lastFps = watch._i * 1000.0 / watch._sw.Elapsed.TotalMilliseconds;
        watch._sw.Restart();
        watch._i = 0;
        return watch;
    }

    

}
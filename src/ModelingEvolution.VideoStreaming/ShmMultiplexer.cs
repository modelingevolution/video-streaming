using System.Runtime.CompilerServices;
using EventPi.SharedMemory;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ModelingEvolution.VideoStreaming.Buffers;
using ModelingEvolution.VideoStreaming.Chasers;

namespace ModelingEvolution.VideoStreaming;

public class ShmMultiplexer : IShmMultiplexer
{ 
    public event EventHandler Disconnected;
    private readonly List<IChaser> _chasers = new();
    private readonly SharedCyclicBuffer _ipBuffer;
    private readonly FrameInfo _info;
    private readonly ILoggerFactory _loggerFactory;
    private readonly VideoPipeline _pipeline;
    private bool _isCanceled;
    private ulong _totalBytesRead;
    private readonly ILogger<ShmMultiplexer> _logger;
  

    public IReadOnlyList<IChaser> Chasers => _chasers.AsReadOnly();
  
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
    public ShmMultiplexer(SharedCyclicBuffer ipBuffer, 
        Func<YuvFrame, Nullable<YuvFrame>, ulong, int, PipeProcessingState, CancellationToken, JpegFrame> handler,
        FrameInfo info, ILoggerFactory loggerFactory, 
        IPartialMatFrameHandler[] matPartialProcessors, 
        IPartialYuvFrameHandler[] yuvPartialProcessors)
    {
        
        _ipBuffer = ipBuffer;
        _info = info;
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<ShmMultiplexer>();
        _pipeline = VideoPipelineBuilder.Create(info, OnGetItem, handler, loggerFactory);

        foreach(var processor in matPartialProcessors) 
            _pipeline.SubscribePartialProcessing(processor.Handle, processor, processor.Should);

        foreach (var processor in yuvPartialProcessors) 
            _pipeline.SubscribePartialProcessing(processor.Handle, processor, processor.Should);
        
        _logger.LogInformation($"Buffered prepared for: {info}");
    }
    
    public int AvgPipelineExecution => _pipeline.Pipeline.AvgPipeExecution;
    
    
    CancellationTokenSource _cts;
    public void Start()
    {
        _cts = new CancellationTokenSource();
        _pipeline.Start(_cts.Token);
        IsEnabled = true;
        IsRunning = true;
    }
    public void SubscribePartialProcessing(Action<MatFrame, Func<MatFrame?>, ulong, CancellationToken, object> action, object state, Func<ulong, bool> every) => _pipeline.SubscribePartialProcessing(action, state, every);
    ulong _fn = 0;
    
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
        Tuple<Stream, string, IShmMultiplexer, CancellationToken> args =
            new Tuple<Stream, string, IShmMultiplexer, CancellationToken>(dst, identifier, this, token);

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
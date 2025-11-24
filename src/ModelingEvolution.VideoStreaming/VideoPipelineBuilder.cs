using Microsoft.Extensions.Logging;

namespace ModelingEvolution.VideoStreaming;

public class VideoPipelineBuilder
{

  
    public static VideoPipeline Create(FrameInfo info, Func<CancellationToken, YuvFrame> getWorkItem,
        Func<YuvFrame, Nullable<YuvFrame>, ulong, int, PipeProcessingState, CancellationToken, JpegFrame> onProcess,
        ILoggerFactory loggerFactory,
        uint pipeBufferCount = 60, int mergeBufferFrameCount = 60 * 4)
    {
        return new VideoPipeline(info, getWorkItem, onProcess, pipeBufferCount, loggerFactory, mergeBufferFrameCount);
    }
}
public class VideoPipeline
{
    private readonly MultiPipeline<YuvFrame, PipeProcessingState, JpegFrame> _pipeline;
    private readonly FrameInfo _frameInfo;
    private readonly ConcurrentCyclicBuffer<JpegFrame> _buffer;
    private readonly uint _pipeBufferCount;
    private readonly List<MatPartialProcess> _matPartialProcesses = new();
    private readonly List<YuvPartialProcess> _yuvPartialProcesses = new();

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
        foreach (var i in _matPartialProcesses)
            _pipeline.SubscribePartialProcessing(i.OnProcess, i, i.Should);
        foreach (var i in _yuvPartialProcesses)
            _pipeline.SubscribePartialProcessing(i.OnProcess, i, i.Should);


        _pipeline.Start(token);
        foreach (var i in _pipeline.Pipes)
            i.Quality = _quality;
    }
    public void SubscribePartialProcessing(Action<MatFrame, Func<MatFrame?>, ulong, CancellationToken, object> action, object state, Func<ulong, bool> every) => _matPartialProcesses.Add(new MatPartialProcess(action, state, every));
    public void SubscribePartialProcessing(Action<YuvFrame, YuvFrame?, ulong, CancellationToken, object> action, object state, Func<ulong, bool> every) => _yuvPartialProcesses.Add(new YuvPartialProcess(action, state, every));


    
    record MatPartialProcess(Action<MatFrame, Func<MatFrame?>, ulong, CancellationToken, object> Action, object State, Func<ulong, bool> Should)
    {
        internal void OnProcess(YuvFrame frame, YuvFrame? nullable, ulong arg3, CancellationToken token, object arg5)
        {
            Action(frame.ToMatFrame(), () => nullable?.ToMatFrame(), arg3, token, State);
        }
    }
    record YuvPartialProcess(Action<YuvFrame, YuvFrame?, ulong, CancellationToken, object> Action, object State, Func<ulong, bool> Should)
    {
        internal void OnProcess(YuvFrame frame, YuvFrame? nullable, ulong arg3, CancellationToken token, object arg5)
        {
            Action(frame, nullable, arg3, token, State);
        }
    }


}

using System.Drawing;
using ModelingEvolution.VideoStreaming;

namespace ModelingEvolution.VideoStreaming.Yolo;

public interface ISegmentationModelRunner<out T> : IDisposable where T : IDisposable
{
    IModelPerformance Performance { get; }
    
    unsafe ISegmentationResult<T> Process(
        YuvFrame* frame, 
        in Rectangle roi,
        in Size dstSize,
        float threshold);
}
public interface IAsyncSegmentationModelRunner<out T> : IDisposable where T : IDisposable
{
    IModelPerformance Performance { get; }
    event EventHandler<ISegmentationResult<ISegmentation>> FrameSegmentationPerformed;

    unsafe void AsyncProcess(
        YuvFrame* frame,
        in Rectangle roi,
        in Size dstSize,
        float threshold);

    void StartAsync();
}
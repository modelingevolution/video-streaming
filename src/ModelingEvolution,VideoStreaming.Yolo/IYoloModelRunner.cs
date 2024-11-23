using System.Drawing;
using ModelingEvolution.VideoStreaming;

namespace ModelingEvolution_VideoStreaming.Yolo;

public interface ISegmentationModelRunner<out T> where T : IDisposable
{
    ModelPerformance Performance { get; }
    unsafe ISegmentationResult<T> Process(
        YuvFrame* frame, 
        in Rectangle roi,
        in Size dstSize,
        float threshold);
}
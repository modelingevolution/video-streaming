using System.Drawing;
using ModelingEvolution.VideoStreaming;

namespace ModelingEvolution_VideoStreaming.Yolo;

public interface IYoloModelRunner<T> where T : IYoloPrediction<T>
{
    ModelPerformance Performance { get; }
    unsafe YoloResult<T> Process(
        YuvFrame* frame, 
        Rectangle* interestArea, 
        float threshold);
}
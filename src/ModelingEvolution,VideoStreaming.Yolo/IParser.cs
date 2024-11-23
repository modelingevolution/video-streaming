using System.Drawing;

namespace ModelingEvolution_VideoStreaming.Yolo;

internal interface IParser<T>
{
    public T[] ProcessTensorToResult(YoloRawOutput output, Rectangle rectangle, float threshold);
}
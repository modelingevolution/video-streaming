using Microsoft.ML.OnnxRuntime.Tensors;

namespace ModelingEvolution_VideoStreaming.Yolo;

internal interface IRawBoundingBoxParser
{
    public T[] Parse<T>(DenseTensor<float> tensor) where T : IRawBoundingBox<T>;
}
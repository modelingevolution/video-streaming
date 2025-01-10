using Microsoft.ML.OnnxRuntime.Tensors;

namespace ModelingEvolution_VideoStreaming.Yolo;

internal readonly ref struct RawParsingContext
{
    public required YoloArchitecture Architecture { get; init; }

    public required DenseTensor<float> Tensor { get; init; }

    public required int Stride1 { get; init; }

    public int NameCount { get; init; }
}
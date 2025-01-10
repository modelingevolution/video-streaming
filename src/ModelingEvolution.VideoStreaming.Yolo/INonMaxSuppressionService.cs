namespace ModelingEvolution.VideoStreaming.Yolo;

internal interface INonMaxSuppressionService
{
    public T[] Suppress<T>(Span<T> boxes, float iouThreshold) where T : IRawBoundingBox<T>;
}
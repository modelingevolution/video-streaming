namespace ModelingEvolution.VideoStreaming.Yolo;

public abstract class YoloPrediction
{
    public required SegmentationClass Name { get; init; }

    public required float Confidence { get; init; }

    public override string ToString()
    {
        return $"{Name.Name} ({Confidence:N})";
    }
}
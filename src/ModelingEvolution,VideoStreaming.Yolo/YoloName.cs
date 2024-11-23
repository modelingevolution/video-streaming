namespace ModelingEvolution_VideoStreaming.Yolo;

public readonly record struct SegmentationClass(int Id, string Name)
{
    public override string ToString()
    {
        return $"{Name}({Id})";
    }
}
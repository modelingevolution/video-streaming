using ModelingEvolution.VideoStreaming.Buffers;

namespace ModelingEvolution.VideoStreaming;

public unsafe readonly struct YuvFrame
{
    public readonly byte* Data;
    public readonly FrameMetadata Metadata;
    public readonly FrameInfo Info;
    public YuvFrame(FrameMetadata metadata, in FrameInfo info, byte* data)
    {
        Data = data;
        Metadata = metadata;
    }
}

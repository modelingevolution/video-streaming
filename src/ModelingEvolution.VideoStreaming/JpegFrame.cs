using ModelingEvolution.VideoStreaming.Buffers;

namespace ModelingEvolution.VideoStreaming;
#pragma warning disable CS4014
public readonly struct JpegFrame
{
    public readonly FrameMetadata Metadata;
    public readonly Memory<byte> Data;
    

    public JpegFrame(FrameMetadata metadata, Memory<byte> data)
    {
        Metadata = metadata;
        Data = data;
    }
}

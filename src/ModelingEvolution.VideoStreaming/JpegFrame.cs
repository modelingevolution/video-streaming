using ModelingEvolution.VideoStreaming.Buffers;
using System.Runtime.InteropServices;

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
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly record struct FrameIdentifier
{
    public readonly ulong FrameId;
    public readonly uint CameraId;

    public FrameIdentifier(ulong frameId, uint cameraId)
    {
        FrameId = frameId;
        CameraId = cameraId;
    }

    public FrameIdentifier()
    {
        
    }

    public override string ToString()
    {
        return $"{CameraId}/{FrameId}";
    }
}
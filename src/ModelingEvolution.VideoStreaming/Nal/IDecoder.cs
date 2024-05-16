namespace ModelingEvolution.VideoStreaming.Nal;

public interface IDecoder
{
    event EventHandler<NALUnit> FrameDecoded;
    void Decode(byte[] data, int read);
    NALType? Decode(byte b);
}
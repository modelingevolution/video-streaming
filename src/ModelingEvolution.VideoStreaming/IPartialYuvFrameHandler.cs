using EventPi.Abstractions;

namespace ModelingEvolution.VideoStreaming;

public interface IPartialYuvFrameHandler
{
    bool Should(ulong seq);
    void Handle(YuvFrame frame,
        YuvFrame? prv,
        ulong seq,
        CancellationToken token, object st);
    void Init(VideoAddress va);
    
}
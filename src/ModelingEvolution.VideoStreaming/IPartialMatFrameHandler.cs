namespace ModelingEvolution.VideoStreaming;

public interface IPartialMatFrameHandler
{
    bool Should(ulong seq);
    void Handle(MatFrame frame,
        Func<MatFrame?> func,
        ulong seq,
        CancellationToken token, object st);
    void Init(VideoAddress va);
}
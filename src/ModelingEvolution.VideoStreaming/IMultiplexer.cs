using ModelingEvolution.VideoStreaming.Chasers;

namespace ModelingEvolution.VideoStreaming;

public interface IMultiplexingStats
{
    ulong TotalReadBytes { get; }
    ulong BufferLength { get; }
    int ClientCount => Chasers.Count;

    ulong TotalTransferred
    {
        get
        {
            ulong u = 0;
            for (int i = 0; i < Chasers.Count; i++)
            {
                try
                {
                    var chaser = Chasers[i];
                    if (chaser != null!)
                        u += chaser.WrittenBytes;
                }
                catch (ArgumentOutOfRangeException)
                {
                    return u;
                }
            }

            return u;
        }
    }

    IReadOnlyList<IChaser> Chasers { get; }
}
public interface IBufferedFrameMultiplexer : IMultiplexingStats
{
    bool IsEnd(int offset);
    int LastFrameOffset { get; }
    ulong ReadFrameCount { get; }
    Memory<byte> Buffer();
    void Disconnect(IChaser chaser);
}
interface IStreamMultiplexer : IMultiplexingStats
{
    Memory<byte> Buffer();
    int ReadOffset { get; }
    void Disconnect(IChaser chaser);
}
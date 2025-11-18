using Microsoft.Extensions.Logging;
using ModelingEvolution.VideoStreaming.Chasers;
using System.Runtime.CompilerServices;

namespace ModelingEvolution.VideoStreaming;

public interface IMultiplexingStats
{
    ulong TotalReadBytes { get; }
    ulong BufferLength { get; }
    int ClientCount => Chasers.Count;
    int AvgPipelineExecution { get; }
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
public interface IShmMultiplexer : IMultiplexingStats
{
    IAsyncEnumerable<Frame> Read(int fps = 30, [EnumeratorCancellation] CancellationToken token = default);
    void Disconnect(IChaser chaser);
}
interface IStreamMultiplexer : IMultiplexingStats
{
    Memory<byte> Buffer();
    int ReadOffset { get; }
    void Disconnect(IChaser chaser);
}
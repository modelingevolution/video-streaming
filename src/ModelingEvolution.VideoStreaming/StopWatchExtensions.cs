using System.Diagnostics;

namespace ModelingEvolution.VideoStreaming;

public static class StopWatchExtensions
{
   
    public static void MeasureReset(this Stopwatch stopwatch, ref ulong counter)
    {
        Interlocked.Add(ref counter, (ulong)stopwatch.ElapsedMilliseconds);
        stopwatch.Restart();
    }
}
using System.Diagnostics;

namespace ModelingEvolution.VideoStreaming.OpenVidCam;

public class PeriodicConsoleWriter(TimeSpan period)
{
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private readonly TimeSpan _period = period;

    public void WriteLine(string text)
    {
        if (_sw.Elapsed <= _period) return;
        Console.WriteLine(text);
        _sw.Restart();
    }
}
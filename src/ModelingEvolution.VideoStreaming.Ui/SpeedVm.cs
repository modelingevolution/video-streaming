using System.Diagnostics;

namespace ModelingEvolution.VideoStreaming.Ui;

class SpeedVm
{
    public SpeedVm()
    {
        _sw = new Stopwatch();
        _sw.Start();
    }
    private readonly Stopwatch _sw;
    private ulong _prv;

    public string Calculate(ulong transferred)
    {
        var delta = transferred - _prv;

        var dt = (ulong)_sw.ElapsedMilliseconds;
        if (dt == 0) return "-";
        _sw.Restart();
        _prv = transferred;
        return $"{(Bytes)(1000 * delta / dt)}/sec";
    }
}
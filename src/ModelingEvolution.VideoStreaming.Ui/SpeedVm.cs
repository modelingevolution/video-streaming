﻿using System.Diagnostics;
using ModelingEvolution.VideoStreaming;
using StreamServer.Services;

namespace StreamServer.Common;

class SpeedVm
{
    public SpeedVm()
    {
        _sw = new Stopwatch();
        _sw.Start();
    }
    private readonly Stopwatch _sw;
    private ulong _prv;

    public string Calculate(ulong transfered)
    {
        var delta = transfered - _prv;

        var dt = (ulong)_sw.ElapsedMilliseconds;
        if (dt == 0) return "-";
        _sw.Restart();
        _prv = transfered;
        return $"{(Bytes)(1000 * delta / dt)}/sec";
    }
}
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.VideoStreaming.Ui.Components;
using ModelingEvolution.VideoStreaming.Ui.Pages;

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

public static class ContainerExtensions
{
    public static IServiceCollection AddVideoStreamingUi(this IServiceCollection services)
    {
        services.AddSingleton<ServerVm>();
        services.AddSingleton<FreeSpaceVmProvider>();
        return services;
    }
}
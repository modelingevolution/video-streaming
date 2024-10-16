using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.VideoStreaming.Ui.Components;
using ModelingEvolution.VideoStreaming.Ui.Pages;

namespace ModelingEvolution.VideoStreaming.Ui;

public static class ContainerExtensions
{
    public static IServiceCollection AddVideoStreamingUi(this IServiceCollection services)
    {
        services.AddSingleton<ServerVm>();
        services.AddScoped<DatasetExplorerVm>();
        services.AddScoped<UploadDatasetVm>();
        services.AddScoped<SwitchCameraButtonVm>();
        services.AddSingleton<FreeSpaceVmProvider>();
        return services;
    }
}
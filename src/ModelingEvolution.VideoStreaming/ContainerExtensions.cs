using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EventPi.Abstractions;
using MicroPlumberd;
using MicroPlumberd.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ModelingEvolution.VideoStreaming
{
    public static class ContainerExtensions
    {
        public static Uri StreamingBindUrl(this IConfiguration configuration) =>
            configuration.GetValue<Uri>("StreamingBindUrl") ?? new Uri("tcp://0.0.0.0:7000");
        public static bool IsSingleVideoStreaming(this IConfiguration configuration) =>
            configuration.GetValue<bool>("IsSingleVideoStreaming");

        public static bool IsStreamingAutostarted(this IConfiguration configuration) =>
            configuration.GetValue<bool>("IsStreamingAutoStarted");
        public static IServiceCollection AddVideoStreaming(this IServiceCollection services, string rootDir)
        {
            services.AddBackgroundServiceIfMissing<VideoStreamingServerStarter>();
            services.AddSingleton<PersistedStreamVm>();
            services.AddSingleton<VideoStreamEventSink>();
            services.AddSingleton<VideoStreamingServer>((sp) =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var streamingBindUrl = configuration.StreamingBindUrl();
                return new VideoStreamingServer(
                    streamingBindUrl.Host,
                    streamingBindUrl.Port,
                    sp.GetRequiredService<ILogger<VideoStreamingServer>>(),
                    sp.GetRequiredService<IPlumber>(),
                    configuration.IsSingleVideoStreaming(),
                    sp.GetRequiredService<VideoStreamEventSink>(),
                    sp.GetRequiredService<IEnvironment>(),
                    sp.GetRequiredService<IConfiguration>(), 
                    sp.GetRequiredService<ILoggerFactory>());
            });
            return services;
        }
    }
    class VideoStreamingServerStarter(VideoStreamingServer srv, IConfiguration config) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (config.IsStreamingAutostarted())
            {
                await Task.Delay(2000, stoppingToken);
                await srv.LoadConfig();
                srv.Start();
            }
        }
    }
}

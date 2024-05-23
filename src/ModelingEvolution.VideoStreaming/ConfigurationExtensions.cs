using Microsoft.Extensions.Configuration;

namespace ModelingEvolution.VideoStreaming;

#pragma warning disable CS4014
public static class ConfigurationExtensions
{
    public static string[] GetConnections(this IConfiguration conf)
    {
        return (conf.GetValue<string>("Connections") ?? String.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);
    }
    public static string VideoStorageDir(this IConfiguration configuration, string root)
    {
        return Path.Combine(root, configuration.GetValue<string>("VideoStorageDir") ?? "videos");
    }
    public static string FfmpegPath(this IConfiguration configuration)
    {
        return configuration.GetValue<string>("FFMpeg") ?? "/usr/bin/ffmpeg";
    }
}
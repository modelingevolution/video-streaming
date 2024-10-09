using Microsoft.Extensions.Configuration;

namespace ModelingEvolution.VideoStreaming;

#pragma warning disable CS4014
public static class VideoStreamingModule
{
    public static string[] GetConnections(this IConfiguration conf)
    {
        return (conf.GetValue<string>("Connections") ?? String.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);
        
    }

    public const string IS_LABELING_MODE_ENABLED_KEY = "IsLabelingModeEnabled";
    public static bool IsLabelingModeEnabled(this IConfiguration configuration)
    {
        return configuration.GetValue<bool>(IS_LABELING_MODE_ENABLED_KEY);
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
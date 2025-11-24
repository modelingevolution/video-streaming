using Microsoft.Extensions.Configuration;

namespace ModelingEvolution.VideoStreaming.OpenVidCam;

static class ConfigurationExtensions
{
    public static int CameraNr(this IConfiguration configuration)
    {
        return configuration.GetValue<int>("Camera", 0);
    }
    public static int Width(this IConfiguration configuration)
    {
        return configuration.GetValue<int>("Width", 1920);
    }
    public static int Height(this IConfiguration configuration)
    {
        return configuration.GetValue<int>("Height", 1080);
    }
    public static int Fps(this IConfiguration configuration)
    {
        return configuration.GetValue<int>("Fps", 25);
    }

    public static bool Display(this IConfiguration configuration)
    {
        return configuration.GetValue<bool>("Display", false);
    }
    public static string StreamName(this IConfiguration configuration)
    {
        return configuration.GetValue<string>("StreamName") ?? "default";
    }
    public static string? RemoteHost(this IConfiguration configuration)
    {
        return configuration.GetValue<string>("RemoteHost") ?? null;
    }
    public static int RemotePort(this IConfiguration configuration)
    {
        return configuration.GetValue<int>("RemotePort", 7000);
    }
}
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace ModelingEvolution.VideoStreaming.Yolo;

public static class Extensions
{
    public static string GetModelAiPath(this IConfiguration config)
    {
        return config.GetValue<string>("ModelAiPath");
    }

    public static float GetAiConfidenceThreshold(this IConfiguration config)
    {
        var str = config.GetValue<string>("ConfidenceThreshold") ?? "0.9";
        return float.Parse(str, CultureInfo.InvariantCulture);
    }
}
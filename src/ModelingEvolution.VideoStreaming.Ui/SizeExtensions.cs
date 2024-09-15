namespace ModelingEvolution.VideoStreaming.Ui;

#pragma warning disable CS4014
public static class StreamUrls
{
    public static string GetStreamUrlPath(string streamName)
    {
        return !string.IsNullOrWhiteSpace(streamName) ? $"stream/{streamName}" : "_content/ModelingEvolution.VideoStreaming.Ui/CameraPreview-1456x1088.jpg";
    }
    public static string GetVectorStreamUrlPath(string streamName)
    {
        return $"vector-stream/{streamName}";
    }
    public static string GetStreamUrl(string host, int port, string streamName)
    {
        string path = GetStreamUrlPath(streamName);
        return $"http://{host}:{port}/{path}";
    }
}
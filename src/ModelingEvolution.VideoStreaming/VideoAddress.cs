namespace ModelingEvolution.VideoStreaming;

public readonly struct VideoAddress
{
    public string? StreamName { get; init; }
    public string Host { get; init; }
    public int Port { get; init; }
    public string Protocol { get; init; }
    public VideoAddress(string protocol, string host, int port, string? streamName = null)
    {
        Host = host;
        Port = port;
        StreamName = streamName;
        Protocol = protocol;
    }
   
}
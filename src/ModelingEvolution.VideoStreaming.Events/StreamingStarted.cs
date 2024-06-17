


using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming.Events;

[OutputStream(Streams.StreamServer)]
public class StreamingStarted 
{
    public static string StreamId(string host)
    {
        if (host == "localhost") return Environment.MachineName;
        return host;
    }
    public StreamingStarted()
    {
        Id = Guid.NewGuid();
    }

    public Guid Id { get; set; }
    public string Source { get; set; }
}
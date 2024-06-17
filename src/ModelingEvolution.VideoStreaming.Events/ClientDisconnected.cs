

using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming.Events;

[OutputStream(Streams.StreamServer)]

public class ClientDisconnected 
{
    public static string StreamId(string host)
    {
        if (host == "localhost") return Environment.MachineName;
        return host;
    }
    public ClientDisconnected()
    {
        Id = Guid.NewGuid();
    }

    public Guid Id { get; set; }
    public string Source { get; set; }
}
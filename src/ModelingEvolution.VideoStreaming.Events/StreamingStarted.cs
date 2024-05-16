


using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming.Events;

[OutputStream(Streams.StreamServer)]
public class StreamingStarted 
{
    public StreamingStarted()
    {
        Id = Guid.NewGuid();
    }

    public Guid Id { get; set; }
    public string Source { get; set; }
}
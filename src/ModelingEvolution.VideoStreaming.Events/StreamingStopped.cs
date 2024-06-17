
using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming.Events;

[OutputStream(Streams.StreamServer)]
public class StreamingStopped 
{

    public StreamingStopped()
    {
        Id = Guid.NewGuid();
    }

    public Guid Id { get; set; }
    public string Source { get; set; }
}
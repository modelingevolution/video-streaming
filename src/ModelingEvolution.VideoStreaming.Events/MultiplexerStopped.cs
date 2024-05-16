using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming.Events;

[OutputStream(Streams.StreamServer)]

public class MultiplexerStopped 
{
    public MultiplexerStopped()
    {
        Id = Guid.NewGuid();
    }

    public Guid Id { get; set; }
}
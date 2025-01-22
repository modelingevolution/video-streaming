using EventPi.Abstractions;
using MicroPlumberd;
using System.Drawing;

namespace ModelingEvolution.VideoStreaming.Recordings;


[OutputStream("Frame")]
public class FrameCreated
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public TimeSpan At { get; init; }
    public ulong Number { get; init; }


}


using EventPi.Abstractions;
using MicroPlumberd;
using System.Drawing;

namespace ModelingEvolution.VideoStreaming.Recordings;

[OutputStream("Frame")]
public class CreateFrame
{
    public Guid Id { get; set; } = Guid.NewGuid();
    

}

[OutputStream("Frame")]
public class FrameCreated
{
    public Guid Id { get; set; } = Guid.NewGuid();
    

}


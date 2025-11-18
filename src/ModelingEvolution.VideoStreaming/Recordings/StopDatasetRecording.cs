using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming.Recordings;

[OutputStream(StreamNames.Recording)]
public class StopRecording
{
    public Guid Id { get; set; } = Guid.NewGuid();

}
using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming.Recordings;

[OutputStream(StreamNames.Recording)]
public class DeleteRecording
{
    public Guid Id { get; set; } = Guid.NewGuid();
}
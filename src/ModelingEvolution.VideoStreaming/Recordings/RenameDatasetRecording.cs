using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming.Recordings;

[OutputStream(StreamNames.Recording)]
public class RenameRecording
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
}
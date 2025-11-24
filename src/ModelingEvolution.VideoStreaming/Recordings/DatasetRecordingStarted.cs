using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming.Recordings;

[OutputStream(StreamNames.Recording)]
public class RecordingStarted
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Failed { get; set; }
    public string Error { get; set; }

}
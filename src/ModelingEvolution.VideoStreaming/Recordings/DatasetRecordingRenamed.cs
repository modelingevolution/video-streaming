using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming.Recordings;

[OutputStream("Dataset")]
public class DatasetRecordingRenamed
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
}
using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming.Recordings;

[OutputStream("Dataset")]
public class DeleteDatasetRecording
{
    public Guid Id { get; set; } = Guid.NewGuid();
}
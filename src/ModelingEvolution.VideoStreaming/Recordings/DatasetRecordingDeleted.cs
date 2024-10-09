using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming.Recordings;

[OutputStream("Dataset")]
public class DatasetRecordingDeleted
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Successfuly { get; set; }
    public string Error { get; set; }
}

[OutputStream("Dataset")]
public class DatasetRecordingPublished
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Successfuly { get; set; }
    public string Error { get; set; }
    
    public int? TaskId { get; set; }
}
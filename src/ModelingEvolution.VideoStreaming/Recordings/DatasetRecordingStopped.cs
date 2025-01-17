using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming.Recordings;

[OutputStream("Dataset")]
public class DatasetRecordingStopped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ulong FrameCount { get; set; }
    public TimeSpan Duration { get; set; }
    public string Folder { get; set; }
    
}

[OutputStream("Dataset")]
public class DatasetRecordingFound
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ulong FrameCount { get; set; }
    public TimeSpan Duration { get; set; }
    public string Folder { get; set; }

}
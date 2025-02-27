using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming.Recordings;

[OutputStream(StreamNames.Recording)]
public class RecordingStopped
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ulong FrameCount { get; set; }
    public TimeSpan Duration { get; set; }
    public string Folder { get; set; }
    
}

[OutputStream(StreamNames.Recording)]
public class RecordingFound
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ulong FrameCount { get; set; }
    public TimeSpan Duration { get; set; }
    public string Folder { get; set; }

}
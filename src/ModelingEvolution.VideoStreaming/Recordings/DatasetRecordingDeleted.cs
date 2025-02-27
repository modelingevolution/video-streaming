using EventPi.Abstractions;
using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming.Recordings;

[OutputStream(StreamNames.Recording)]
public class RecordingDeleted
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Successfuly { get; set; }
    public string Error { get; set; }
}

public static class StreamNames
{
    public const string Recording = "Recording";
}

// Id: VideoRecordingIdentifier
[OutputStream(StreamNames.Recording)]
public record RecordingPublished
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public bool Successfully { get; init; }
    public string Error { get; init; }
    public string Name { get; init; }
    public int Frames { get; init; }
    
    public TimeSpan Duration { get; init; }
    public DateTimeOffset RecordingStarted { get; set; }
}
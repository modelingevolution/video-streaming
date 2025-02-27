using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming.Recordings;

[OutputStream("Datasets")]
public class StartRecording 
{
    public Guid Id { get; set; } = Guid.NewGuid();

}
[OutputStream("Datasets")]
public class FindMissingRecordings
{
    public Guid Id { get; set; } = Guid.NewGuid();

}
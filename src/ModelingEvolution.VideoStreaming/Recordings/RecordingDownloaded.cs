using EventPi.Abstractions;
using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming.Recordings;

[OutputStream("Recording")]
public class RecordingDownloaded
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecordingId{ get; set; }
    public string RecordingName { get; set; }
    public HostName Device {  get; set; }
    public string Destination { get; set; }

}
[OutputStream("Recording")]
public class RecordingDownloadingStarted
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecordingId { get; set; }
    public string RecordingName { get; set; }
    public HostName Device { get; set; }
    public string Destination { get; set; }
}

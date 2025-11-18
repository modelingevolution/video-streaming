using MicroPlumberd;
using System.Collections.Concurrent;

namespace ModelingEvolution.VideoStreaming.Recordings;

[OutputStream(StreamNames.Recording)]
public class RecordingRenamed
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; }
}


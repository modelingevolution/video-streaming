using System.ComponentModel.DataAnnotations;
using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming.Recordings;

[OutputStream(StreamNames.Recording)]
public class RenameRecording
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required, MinLength(1)]
    public string Name { get; set; }
}
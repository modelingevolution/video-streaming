using System.ComponentModel.DataAnnotations;
using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming.Recordings;

[OutputStream(StreamNames.Recording)]
public class PublishRecording
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int ProjectId { get; set; }
    
    [Required]
    [MinLength(1)]
    public string Subset { get; set; }
    public ulong StartFrame { get; set; }
    public ulong EndFrame { get; set; }
    public int Every { get; set; }

    [Required]
    [MinLength(1)]
    public string Name { get; set; }

    public IEnumerable<ulong> CalculateSet(IEnumerable<ulong> frames)
    {
        int c = 0;
        foreach (var i in frames)
        {
            if ((c++ % Every) == 0)
            {
                if (i < StartFrame) continue;
                if (i > EndFrame) break;

                yield return i;
            }
        }
    }
}
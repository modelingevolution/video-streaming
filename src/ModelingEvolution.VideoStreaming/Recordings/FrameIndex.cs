using System.Text.Json.Serialization;

namespace ModelingEvolution.VideoStreaming.Recordings;

public record FrameIndex(ulong Start, ulong Size, long TimeStamp)
{
    [JsonIgnore]
    public DateTime Created => DateTime.FromBinary(TimeStamp);
}
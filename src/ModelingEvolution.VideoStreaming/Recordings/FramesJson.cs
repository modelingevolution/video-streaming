using System.Text.Json.Serialization;

namespace ModelingEvolution.VideoStreaming.Recordings;

// Frame-Number (Sequence) : FrameIndex
public class FramesJson : SortedList<ulong, FrameIndex>
{
    public ulong GetNextKey(ulong key)
    {
        var nextKey = key;
        var keys = Keys;
        var index = keys.IndexOf(key);

        if (index >= 0 && index < keys.Count - 1) 
            nextKey = keys[index + 1];

        return nextKey;
    }

    public ulong GetPrevKey(ulong key)
    {
        var prvKey = key;
        var keys = Keys;
        var index = keys.IndexOf(key);

        if (index > 0) prvKey = keys[index - 1];

        return prvKey;
    }
}

public class RecodingJson
{
    public string Caps { get; set; }
    // Space for other constant metadata, like camera model, versions etc.
    public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    public FramesJson Index { get; set; }
    
}
public record FrameIndex(ulong Start, ulong Size, long TimeStamp)
{
    [JsonIgnore]
    public DateTime Created => DateTime.FromBinary(TimeStamp);
}
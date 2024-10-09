namespace ModelingEvolution.VideoStreaming.Recordings;

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
using EventPi.Abstractions;
using System.Collections.Concurrent;

namespace ModelingEvolution.VideoStreaming.Recordings;

public class UnmergedRecordingManager : IUnmergedRecordingManager
{
    private readonly ConcurrentDictionary<VideoRecordingDevice, UnmergedRecordingService> _index = new();


    public void Register(VideoRecordingDevice va, UnmergedRecordingService srv)
    {
        _index.TryAdd(va, srv);
    }
    public IUnmergedRecordingService? Get(VideoRecordingDevice id)
    {
        return _index.TryGetValue(id, out var srv) ? srv : null;
    }

    public void Unregister(VideoRecordingDevice address)
    {
        _index.TryRemove(address, out _);
    }
}
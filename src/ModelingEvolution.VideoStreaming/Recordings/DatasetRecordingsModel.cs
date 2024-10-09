using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using EventPi.Abstractions;
using MicroPlumberd;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace ModelingEvolution.VideoStreaming.Recordings;


[EventHandler]
public partial class DatasetRecordingsModel(IConfiguration config, IWebHostingEnv env)
{
    private readonly ObservableCollection<DatasetRecording> _index = new();
    private readonly ConcurrentDictionary<Guid, DatasetRecording> _byId = new();

    
    public IList<DatasetRecording> Items => _index;
    private async Task Given(Metadata m, DatasetRecordingStopped ev)
    {
        var id = m.StreamId<VideoRecordingIdentifier>();
        if (!_byId.ContainsKey(id))
        {
            var item = new DatasetRecording(id, Path.Combine(config.VideoStorageDir(env.WwwRoot), ev.Folder), ev.Duration, ev.FrameCount);
            _index.Add(item);
            _byId.TryAdd(id, item);
        }
    }

    public DatasetRecording? GetById(Guid id) => _byId.TryGetValue(id, out var r) ? r : null;
    private async Task Given(Metadata m, DatasetRecordingRenamed ev)
    {
        var id = m.StreamId<VideoRecordingIdentifier>();
        if (_byId.TryGetValue(id, out var r)) 
            r.Name = ev.Name;
    }
    private async Task Given(Metadata m, DatasetRecordingDeleted ev)
    {
        var id = m.StreamId<VideoRecordingIdentifier>();
        if (_byId.Remove(id, out var r)) 
            _index.Remove(r);
    }

    private async Task Given(Metadata m, DatasetRecordingPublished ev)
    {
        var id = m.StreamId<VideoRecordingIdentifier>();
        if (_byId.TryGetValue(id, out var r))
        {
            if (ev.Successfuly)
            {
                r.PublishState = PublishState.Success;
                r.PublishError = null;
                r.PublishedDate = m.Created().Value.DateTime;
            }
            else
            {
                r.PublishState = PublishState.Failed;
                r.PublishError = ev.Error;
            }
        }
    }
}
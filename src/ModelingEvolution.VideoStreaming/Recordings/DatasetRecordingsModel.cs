using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using EventPi.Abstractions;
using MicroPlumberd;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace ModelingEvolution.VideoStreaming.Recordings;


    [EventHandler]
public partial class RecordingsModel
{
    private readonly ObservableCollection<Recording> _index = new();
    private readonly ConcurrentDictionary<Guid, Recording> _byId = new();
    
    public IList<Recording> Items => _index;
    
    private DateTime _lastExecution;
    
    private async Task Given(Metadata m, RecordingStopped ev)
    {
        _lastExecution = DateTime.Now;
        
        var id = m.StreamId<VideoRecordingIdentifier>();
        if (!_byId.ContainsKey(id))
        {
            var item = new Recording(id, Path.Combine(_config.VideoStorageDir(_env.WwwRoot), ev.Folder), ev.Duration, ev.FrameCount);
            _index.Add(item);
            _byId.TryAdd(id, item);
        }
    }

    private bool _catchup = false;
    private readonly IConfiguration _config;
    private readonly IWebHostingEnv _env;
    private readonly ICommandBus _cmdBus;
    private readonly IEnvironment _eh;

    public RecordingsModel(IConfiguration config, IWebHostingEnv env, ICommandBus cmdBus, IEnvironment eh)
    {
        _config = config;
        _env = env;
        _cmdBus = cmdBus;
        _eh = eh;
        _lastExecution = DateTime.Now;
        Task.Run(CheckCatchUp);
    }

    private void CheckCatchUp()
    {
        while (DateTime.Now.Subtract(_lastExecution) < TimeSpan.FromSeconds(15))
            Task.Delay(1000);
        
        CatchtUp();
    }
    void CatchtUp()
    {
        if (_catchup) return;
        
        _catchup = true;
        var id = new VideoRecordingDevice() { CameraNumber=int.MaxValue, HostName = _eh.HostName};
        _ = _cmdBus.SendAsync(id, new FindMissingRecordings());
    }
    private async Task Given(Metadata m, RecordingFound ev)
    {
        _lastExecution = DateTime.Now;
        var id = m.StreamId<VideoRecordingIdentifier>();
        if (!_byId.ContainsKey(id))
        {
            var item = new Recording(id, Path.Combine(_config.VideoStorageDir(_env.WwwRoot), ev.Folder), ev.Duration, ev.FrameCount);
            _index.Add(item);
            _byId.TryAdd(id, item);
        }
    }

    public Recordings.Recording? GetById(Guid id) => _byId.TryGetValue(id, out var r) ? r : null;
    private async Task Given(Metadata m, RecordingRenamed ev)
    {
        _lastExecution = DateTime.Now;
        var id = m.StreamId<VideoRecordingIdentifier>();
        if (_byId.TryGetValue(id, out var r)) 
            r.Name = ev.Name;
    }
    private async Task Given(Metadata m, RecordingDeleted ev)
    {
        _lastExecution = DateTime.Now;
        var id = m.StreamId<VideoRecordingIdentifier>();
        if (_byId.Remove(id, out var r)) 
            _index.Remove(r);
    }

    private async Task Given(Metadata m, RecordingPublished ev)
    {
        _lastExecution = DateTime.Now;
        var id = m.StreamId<VideoRecordingIdentifier>();
        if (_byId.TryGetValue(id, out var r))
        {
            if (ev.Successfully)
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
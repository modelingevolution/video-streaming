using MicroPlumberd;
using MicroPlumberd.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelingEvolution.VideoStreaming.CVat;
using System.Text.Json;
using EventPi.Abstractions;

namespace ModelingEvolution.VideoStreaming.Recordings;

[CommandHandler]
public partial class DatasetRecordingCommandHandler(IUnmergedRecordingManager manager, IPlumber plumber, IConfiguration config,
    IWebHostingEnv env, IEnvironment host,
    ILogger<DatasetRecordingCommandHandler> logger, DatasetRecordingsModel model, VideoImgFrameProvider frameProvider, ICVatClient cvat)
{

    public async Task Handle(VideoRecordingDevice dev, FindMissingDatasetRecordings cmd)
    {
        var directories = Directory.GetDirectories(config.VideoStorageDir(env.WwwRoot));
        foreach (var directory in directories)
        {
            var folder = Path.GetFileName(directory);

            if (model.Items.Any(x => x.DirectoryName == folder))
                return;
            
            var dataFile = Path.Combine(directory, "stream.mjpeg");
            var indexFile = Path.Combine(directory, "index.json");

            if (!File.Exists(dataFile) || !File.Exists(indexFile))
            {
                logger.LogWarning($"Found a folder that should not be there. No relevant files were found. Skipping: {folder}");
                continue;
            }

            try
            {
                var readAllTextAsync = await File.ReadAllTextAsync(indexFile);
                var ix = JsonSerializer.Deserialize<FramesJson>(readAllTextAsync);
                var lastFrame = ix.Values.Last();
                var firstFrame = ix.Values.First();
                var duration = lastFrame.Created.Subtract(firstFrame.Created);

                VideoRecordingIdentifier vri = new VideoRecordingIdentifier()
                {
                    CameraNumber= int.MaxValue,
                    CreatedTime = firstFrame.Created,
                    HostName = host.HostName
                };
                var ev = new DatasetRecordingFound()
                {
                    Duration = duration, 
                    Folder = folder, 
                    FrameCount = (ulong)ix.Keys.Count
                };
                await plumber.AppendEvent(ev, vri);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, $"Could not index folder {folder}.");
            }
        }
    }
    public async Task Handle(VideoRecordingDevice dev, StartDatasetRecording cmd)
    {
        try
        {
            var srv = manager.Get(dev);
            if (srv != null)
            {
                var id = await srv.Start();
                var ev = new DatasetRecordingStarted();
         
                await plumber.AppendEvent(ev, id);
            }
            else logger.LogWarning("No device found: " + dev.ToString());
        }
        catch (Exception ex)
        {
            var ev = new DatasetRecordingStarted() { Error = ex.Message, Failed = true };
            await plumber.AppendEvent(ev, dev);
        }
    }
    public async Task Handle(VideoRecordingIdentifier dev, RenameDatasetRecording cmd)
    {
        await plumber.AppendEvent(new DatasetRecordingRenamed() { Name = cmd.Name }, dev);
    }
    public async Task Handle(VideoRecordingIdentifier dev, DeleteDatasetRecording cmd)
    {
        try
        {
            
            var i = model.GetById(dev);
            frameProvider.Close(Path.GetFileName(i.DirectoryFullPath));
            var dir = i.DirectoryFullPath; 
            Directory.Delete(dir, true);
            await plumber.AppendEvent(new DatasetRecordingDeleted() { Successfuly = true }, dev);
        }
        catch(Exception ex)
        {
            await plumber.AppendEvent(new DatasetRecordingDeleted() { Successfuly = false, Error = ex.Message}, dev);
        }
        
    }
    public async Task Handle(VideoRecordingIdentifier dev, PublishDatasetRecording cmd)
    {
        var set = model.GetById(dev);
        var doc = frameProvider[set.DirectoryName];
        try
        {
            var baseUrl = new Uri(config.PublicUrl());
            var urls = cmd.CalculateSet(doc.Keys).Select(x => new Uri(baseUrl, $"/video/{set.DirectoryName}/{x}.jpeg").ToString()).ToArray();
            
            var id = await cvat.CreateTask(cmd.Name ?? set.Name, cmd.Subset, cmd.ProjectId);
            await cvat.AttachTaskData(id.Id, 80, true, urls);

            await plumber.AppendEvent(new DatasetRecordingPublished() { Successfuly = true, TaskId = id.Id }, dev);
        }
        catch (Exception ex)
        {
            await plumber.AppendEvent(new DatasetRecordingPublished() { Successfuly = false, Error = ex.Message}, dev);
        }

        
    }
    public async Task Handle(VideoRecordingDevice dev, StopDatasetRecording cmd)
    {
        var srv = manager.Get(dev);
        if (srv != null)
        {
            var r = await srv.Stop();
            var ev = new DatasetRecordingStopped() { Duration = r.Duration, Folder = r.Folder, FrameCount = r.FrameCount};
            await plumber.AppendEvent(ev, r.Id);
        }
        else
            logger.LogWarning("No device found: " + dev.ToString());
    }
}
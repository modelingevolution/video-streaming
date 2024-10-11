using Emgu.CV.Dnn;
using MicroPlumberd;
using MicroPlumberd.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelingEvolution.VideoStreaming.CVat;

namespace ModelingEvolution.VideoStreaming.Recordings;

[CommandHandler]
public partial class DatasetRecordingCommandHandler(IUnmergedRecordingManager manager, IPlumber plumber, IConfiguration configuraiton,
    ILogger<DatasetRecordingCommandHandler> logger, DatasetRecordingsModel model, VideoImgFrameProvider frameProvider, ICVatClient cvat)
{
    

    public async Task Handle(VideoRecordingDevice dev, StartDatasetRecording cmd)
    {
        try
        {
            logger.LogDebug("Start recording dataset - 1");
            var srv = manager.Get(dev);
            logger.LogDebug("Start recording dataset - 2");
            if (srv != null)
            {
                logger.LogDebug("Start recording dataset - 3");
                var id = await srv.Start();
                var ev = new DatasetRecordingStarted();
                logger.LogDebug("Start recording dataset - 4");
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
            var baseUrl = new Uri(configuraiton.PublicUrl());
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
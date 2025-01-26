using EventPi.Abstractions;
using Microsoft.Extensions.Configuration;
using System.Buffers;
using System.Collections.Concurrent;
using System.Text.Json;
using ModelingEvolution.Drawing;
namespace ModelingEvolution.VideoStreaming.Recordings;


public class VideoRecordingFrameProvider
{

    public virtual Task<Memory<byte>> GetFrame(in FrameId frame)
    {
        return GetFrame(_locator.GetPath(frame.Recording).Directory, frame.FrameNumber);
    }
    public async Task<Memory<byte>> GetFrame(string? recordingPath, ulong frame)
    {
        var frames = this[recordingPath];
        if (frames == null)
            return Memory<byte>.Empty;


        if (!frames.TryGetValue(frame, out var frameIndex))
            return Memory<byte>.Empty;

        var buffer = new byte[frameIndex.Size];
        var fileStreams = GetStreams(recordingPath);
        var fileStream = fileStreams.Take();

        fileStream.Seek((long)frameIndex.Start, SeekOrigin.Begin);
        await fileStream.ReadAsync(buffer, 0, buffer.Length);

        fileStreams.Add(fileStream);

        return buffer;
    }
    public async Task<IMemoryOwner<byte>?> GetFrameShared(string? fileName, ulong frame)
    {
        var frames = this[fileName];
        if (frames == null)
            return null;


        if (!frames.TryGetValue(frame, out var frameIndex))
            return null;

        var buffer = MemoryPool<byte>.Shared.Rent((int)frameIndex.Size);
        var fileStreams = GetStreams(fileName);
        var fileStream = fileStreams.Take();

        fileStream.Seek((long)frameIndex.Start, SeekOrigin.Begin);
        await fileStream.ReadAsync(buffer.Memory);

        fileStreams.Add(fileStream);

        return buffer;
    }

    private BlockingCollection<FileStream> GetStreams(string fileName)
    {
        var fileStreams = _fileStreamPool.GetOrAdd(fileName, _ =>
        {
            var path = Path.Combine(_videoStorage, fileName, "stream.mjpeg");
            var bag = new BlockingCollection<FileStream>()
            {
                File.OpenRead(path),
                File.OpenRead(path)
            };
            return bag;
        });
        return fileStreams;
    }

    public void Close(string fileName)
    {
        fileName = Path.GetFileName(fileName);
        if (!_fileStreamPool.TryGetValue(fileName, out var pool)) return;
        foreach (var i in pool) i.Dispose();
    }
    private readonly ConcurrentDictionary<string, BlockingCollection<FileStream>> _fileStreamPool = new();
    private readonly ConcurrentDictionary<string, FramesJson> _index = new();
    private readonly string _videoStorage;
    private readonly IVideoRecordingLocator _locator;

    public VideoRecordingFrameProvider()
    {

    }
    public VideoRecordingFrameProvider(string datasetDirectory, IVideoRecordingLocator locator)
    {
        _videoStorage = datasetDirectory;
        _locator = locator;
    }
    public FramesJson this[string fileName]
    {
        get
        {

            return _index.GetOrAdd(fileName, x =>
            {

                if (!Directory.Exists(x)) return new FramesJson();
                var jsonPath = Path.Combine(x, "index.json");
                var doc = JsonSerializer.Deserialize<FramesJson>(File.ReadAllText(jsonPath));
                return doc;
            });
        }
    }
}

using System.Collections.Concurrent;
using System.Text.Json;
using EventPi.Abstractions;
using Microsoft.Extensions.Configuration;

namespace ModelingEvolution.VideoStreaming.Recordings;

public class VideoImgFrameProvider
{
    public async Task<Memory<byte>> GetFrame(string? fileName, ulong frame)
    {
        var frames = this[fileName];
        if (frames == null)
            return Memory<byte>.Empty;
        

        if (!frames.TryGetValue(frame, out var frameIndex))
            return Memory<byte>.Empty;

        var buffer = new byte[frameIndex.Size];
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
        var fileStream = fileStreams.Take();

        fileStream.Seek((long)frameIndex.Start, SeekOrigin.Begin);
        await fileStream.ReadAsync(buffer, 0, buffer.Length);
        
        fileStreams.Add(fileStream);

        return buffer;
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

    public VideoImgFrameProvider(IConfiguration _configuration, IWebHostingEnv _env)
    {
        this._videoStorage = _configuration.VideoStorageDir(_env.WwwRoot);

    }
    public FramesJson this[string fileName]
    {
        get
        {

            return _index.GetOrAdd(fileName, x =>
            {
                var fullFile = Path.Combine(_videoStorage, x);
                if (!Directory.Exists(fullFile)) return new FramesJson();
                var jsonPath = Path.Combine(fullFile, "index.json");
                var doc = JsonSerializer.Deserialize<FramesJson>(File.ReadAllText(jsonPath));
                return doc;
            });
        }
    }
}
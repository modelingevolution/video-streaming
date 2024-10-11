using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;

using EventPi.Abstractions;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelingEvolution.VideoStreaming.Buffers;
using ModelingEvolution.VideoStreaming.LibJpegTurbo;

namespace ModelingEvolution.VideoStreaming.Recordings;

public class UnmergedRecordingService : IPartialYuvFrameHandler, IDisposable, IUnmergedRecordingService
{
    private readonly UnmergedRecordingManager _manager;
    private readonly ILogger<UnmergedRecordingService> _logger;
    private CancellationTokenSource _cts;
    private readonly string _ffmpegExec;
    private readonly string _dataDir;
    private string? _outputDirectory;
    private readonly ConcurrentBag<JpegEncoder> _encPool = new();
    private readonly Channel<FrameToWrite> _sink = Channel.CreateUnbounded<FrameToWrite>();
    private const int TIME_STAMP_TABLE_SIZE = 120;
    private readonly DateTime[] _timeStamps = new DateTime[TIME_STAMP_TABLE_SIZE];
    private VideoRecordingIdentifier _currentRecording;

    public UnmergedRecordingService(UnmergedRecordingManager manager, IConfiguration configuration, IWebHostingEnv he, ILogger<UnmergedRecordingService> logger)
    {
        _manager = manager;
        _logger = logger;
        _ffmpegExec = configuration.FfmpegPath();
        if (!File.Exists(_ffmpegExec))
            logger.LogWarning("FFMPEG executable not found at: {ffmpeg}", _ffmpegExec);
        _dataDir = configuration.VideoStorageDir(he.WwwRoot);
        if (!Directory.Exists(_dataDir))
            Directory.CreateDirectory(_dataDir);

    }

    public bool ShouldRun { get; private set; }
    public VideoAddress Address { get; private set; }
    
    public async Task<VideoRecordingIdentifier> Start()
    {
        if (ShouldRun) return _currentRecording;
        _logger.LogDebug("Start - 1");
        _outputDirectory = GetOutputDirectory(Address, Address.Tags, out var id);
        _logger.LogDebug("Start - 2");
        _cts = new CancellationTokenSource();
        ShouldRun = true;
        _logger.LogDebug("Start - 3");
        _ = Task.Factory.StartNew(RunWriter, TaskCreationOptions.LongRunning);
        _currentRecording = id;
        return id;
    }

    private async Task RunWriter()
    {
        _logger.LogDebug("Run writter");
        await WriteFiles();
    }

    private async Task WriteFiles()
    {
        await using var dataStream = File.OpenWrite(Path.Combine(_outputDirectory, "stream.mjpeg"));
        await using var indexStream = File.OpenWrite(Path.Combine(_outputDirectory, "index.json"));
        await using var indexWriter = new StreamWriter(indexStream);

        await indexWriter.WriteLineAsync("{");
        try
        {
            bool first = true;
            string prefix = "";
            await foreach (var i in _sink.Reader.ReadAllAsync(_cts.Token))
            {
                var timeStamp = _timeStamps[i.Frame.Metadata.FrameNumber % TIME_STAMP_TABLE_SIZE];
                long position = dataStream.Position;
                await dataStream.WriteAsync(i.Frame.Data);
                var line = $"{prefix}{Environment.NewLine}\"{i.Frame.Metadata.FrameNumber}\": {{ \"Start\":{position}, \"Size\": {i.Frame.Data.Length}, \"TimeStamp\": {timeStamp.ToBinary()} }}";
                
                await indexWriter.WriteAsync(line);
                i.Buffer.Dispose();

                if (first)
                    prefix = ",";
                first = false;

                Interlocked.Increment(ref _count);

                if (!ShouldRun)
                {
                    break;
                }

            }
        }
        catch (OperationCanceledException ex)
        {
            
        }
        await indexWriter.WriteLineAsync("}");
        IsRunning = false;
    }

    
    
    private string GetOutputDirectory(VideoAddress address, HashSet<string> tags, out VideoRecordingIdentifier id)
    {
        StringBuilder outFile = new StringBuilder();
        string extension = "mp4";
        if (tags.Any())
        {
            var first = tags.First();
            outFile.Append(first);
        }
        else
        {
            outFile.Append(!string.IsNullOrWhiteSpace(address.StreamName) ? address.StreamName : address.Host);
        }

        var n = DateTime.Now;
        var dateSegment = n.ToString("yyyyMMdd");
        var timeSpan = n.TimeOfDay;

        if (timeSpan.Days > 0)
            outFile.Append($".{dateSegment}.{timeSpan.Days}.{timeSpan.Hours:D2}{timeSpan.Minutes:D2}{timeSpan.Seconds:D2}");
        else
            outFile.Append($".{dateSegment}.{timeSpan.Hours:D2}{timeSpan.Minutes:D2}{timeSpan.Seconds:D2}");

        if (!Directory.Exists(_dataDir))
            Directory.CreateDirectory(_dataDir);
        string outputFilePath = Path.Combine(_dataDir, outFile.ToString());
        if (!Directory.Exists(outputFilePath))
            Directory.CreateDirectory(outputFilePath);
        _logger.LogInformation("Labeling will be saved at: " + outputFilePath);

        VideoRecordingIdentifier tmp = address;
        id = tmp with { CreatedTime = n };
        
        return outputFilePath;
    }

    public record StopResult(VideoRecordingIdentifier Id, ulong FrameCount, TimeSpan Duration, string Folder);
    public async Task<StopResult> Stop()
    {
        var duration = DateTime.Now.Subtract(this._currentRecording.CreatedTime);
        
        ShouldRun = false;
        _cts.Cancel();
        
        while (IsRunning) 
            await Task.Delay(100);
        
        
        StopResult r = new StopResult(_currentRecording, _count, duration, Path.GetFileName(_outputDirectory));
        _firstSeq = ulong.MaxValue;
        _count = 0;
        return r;
    }

    
    private ulong _count = 0;
    private ulong _firstSeq = ulong.MaxValue;
    private ulong _lastEnqueued;
    public bool IsRunning { get; set; }
    public bool Should(ulong seq)
    {
        _timeStamps[seq % TIME_STAMP_TABLE_SIZE] = DateTime.Now;
        var ret = ShouldRun;
        if (ret)
        {
            _lastEnqueued = seq;
            _firstSeq = Math.Min(_firstSeq, seq);
            IsRunning = true;
        }
       
        return ret;
    }


    
    

    private record struct FrameToWrite(JpegFrame Frame, ManagedArray<byte> Buffer);
    
    public unsafe void Handle(YuvFrame frame, YuvFrame? prv, ulong seq, CancellationToken token, object st)
    {
        if (!ShouldRun) return;
        
        //Span<byte> span = MemoryMarshal.CreateSpan(ref *ptr, (int)frame.Metadata.FrameSize);
        if (!_encPool.TryTake(out var encoder)) return;

        ManagedArray<byte> array = new ManagedArray<byte>(frame.Info.Yuv420);
        nint ptr = (nint)frame.Data;
        var buffer = array.GetBuffer();
        var size = encoder.Encode(ptr, buffer);
        JpegFrame f = new JpegFrame(frame.Metadata, buffer.AsMemory(0, (int)size));
        _sink.Writer.TryWrite(new FrameToWrite(f, array));
        _encPool.Add(encoder);
    }

    public void Init(VideoAddress va)
    {
        Address = va;
        _manager.Register(va, this);
        var r = (Resolution)va.Resolution;
        _encPool.Add(JpegEncoderFactory.Create(r.Width, r.Height, 80, 0));
    }



    public void Dispose()
    {
        _manager.Unregister(Address);
    }
}
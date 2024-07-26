using System.Net.WebSockets;
using EventPi.SharedMemory;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ModelingEvolution.VideoStreaming.Nal;

namespace ModelingEvolution.VideoStreaming;

#pragma warning disable CS4014
public class VideoSharedBufferReplicator : IVideoStreamReplicator
{
    private SharedCyclicBuffer? _buffer;
    private SharedBufferMultiplexer2? _multiplexer;
    private readonly VideoStreamEventSink _evtSink;
    private readonly ILogger<VideoSharedBufferReplicator> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly FrameInfo _info;
    public string SharedMemoryName { get; private set; }

    public VideoSharedBufferReplicator(VideoAddress va, 
        FrameInfo info,
        VideoStreamEventSink evtSink, 
        ILogger<VideoSharedBufferReplicator> logger,
        ILoggerFactory loggerFactory)
    {
        SharedMemoryName = va.StreamName;
        _info = info;
        _evtSink = evtSink;
        _logger = logger;
        _loggerFactory = loggerFactory;
        VideoAddress = va;
    }

    public event EventHandler<StoppedEventArgs>? Stopped;
    public IMultiplexingStats MultiplexingStats => _multiplexer;
    public string Host { get; } = Environment.MachineName;
    public DateTime Started { get; private set; }
    public VideoAddress VideoAddress { get; }

    public IVideoStreamReplicator Connect()
    {
        _logger.LogInformation($"Shared memory: {SharedMemoryName}, total size: {_info.Yuv420*120} bytes, frame: {_info.Yuv420} bytes for {_info}");
        _buffer = new SharedCyclicBuffer(120, _info.Yuv420,  SharedMemoryName, OpenMode.CreateNewForReading); // ~180MB

        //_multiplexer = new SharedBufferMultiplexer(_buffer, _info, _loggerFactory);
        _multiplexer = new SharedBufferMultiplexer2(_buffer, 
            VideoAddress.VideoSource == VideoSource.File ?
            FrameProcessingHandlers.OnProcess:
            FrameProcessingHandlers.OnProcessHdr, _info, _loggerFactory);
        _multiplexer.Start();
        Started = DateTime.Now;
        _evtSink.OnStreamingStarted(VideoAddress);
        return this;
    }

    public async Task ReplicateTo(HttpContext ns, string? identifier, CancellationToken token = default)
    {
        await _multiplexer!.Chase(ns, identifier, token);
    }
    public async Task ReplicateTo(WebSocket ns, string? identifier)
    {
        throw new NotImplementedException();
    }
    public void ReplicateTo(Stream ns, string? identifier, CancellationToken token = default)
    {
        _multiplexer!.Chase(ns, identifier, token);
    }

    public bool Is(string name)
    {
        return this.VideoAddress.Host.Equals(name, StringComparison.CurrentCultureIgnoreCase) ||
               string.Equals(this.VideoAddress.StreamName, name, StringComparison.CurrentCultureIgnoreCase);
    }

    public void Dispose()
    {
        _evtSink.OnStreamingDisconnected(VideoAddress);
        _buffer?.Dispose();

    }

    public void Stop()
    {
        _multiplexer?.Stop();
        Stopped?.Invoke(this, new StoppedEventArgs(StoppedReason.DeletedByUser));
    }
}
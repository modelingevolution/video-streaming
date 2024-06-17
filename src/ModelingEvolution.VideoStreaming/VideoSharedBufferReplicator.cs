using System.Net.WebSockets;
using EventPi.SharedMemory;
using Microsoft.AspNetCore.Http;
using ModelingEvolution.VideoStreaming.Nal;

namespace ModelingEvolution.VideoStreaming;

#pragma warning disable CS4014
public class VideoSharedBufferReplicator : IVideoStreamReplicator
{
    private SharedCyclicBuffer? _buffer;
    private SharedBufferMultiplexer? _multiplexer;
    private readonly VideoStreamEventSink _evtSink;
    private readonly FrameInfo _info;
    public string SharedMemoryName { get; private set; }

    public VideoSharedBufferReplicator(string sharedMemoryName, FrameInfo info, 
        VideoStreamEventSink evtSink)
    {
        SharedMemoryName = sharedMemoryName;
        _info = info;
        _evtSink = evtSink;
        VideoAddress = new VideoAddress(VideoProtocol.Mjpeg, streamName: sharedMemoryName);
    }

    public event EventHandler? Stopped;
    public IMultiplexingStats MultiplexingStats => _multiplexer;
    public string Host { get; } = Environment.MachineName;
    public DateTime Started { get; private set; }
    public VideoAddress VideoAddress { get; }

    public IVideoStreamReplicator Connect()
    {
        _buffer = new SharedCyclicBuffer(60, _info.Yuv420,  SharedMemoryName); // ~180MB
        _multiplexer = new SharedBufferMultiplexer(_buffer, _info);
        _multiplexer.Start();
        Started = DateTime.Now;
        _evtSink.OnStreamingStarted(VideoAddress);
        return this;
    }

    public async Task ReplicateTo(HttpContext ns, string? identifier, CancellationToken token = default)
    {
        _multiplexer!.Chase(ns, identifier, token);
    }
    public async Task ReplicateTo(WebSocket ns, string? identifier)
    {
        throw new NotImplementedException();
    }
    public void ReplicateTo(Stream ns, string? identifier)
    {
        throw new NotImplementedException();
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
}
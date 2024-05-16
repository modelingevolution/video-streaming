using ModelingEvolution.Observable;
using StreamServer.Common;

namespace ModelingEvolution.VideoStreaming.Ui.Pages;

public class ReplicatorVm : IViewFor<VideoStreamReplicator>, IEquatable<ReplicatorVm>
{
        
    private readonly SpeedVm _inTransferSpeed;
    private readonly SpeedVm _outTransferSpeed;
    private readonly VideoStreamReplicator _source;
    public StreamMultiplexer StreamMultiplexer => _source.StreamMultiplexer;
    public Bytes TotalBytes => StreamMultiplexer.TotalReadBytes;
    public string Host => _source.Host;

    public int Port => _source.Port;
    public string Address =>
        !string.IsNullOrWhiteSpace(_source.StreamName) ? 
            $"{_source.Protocol}://{Host}:{Port}/{_source.StreamName}" : 
            $"{_source.Protocol}://{Host}:{Port}";

    public string ViewerUrl
    {
        get { return $"/viewer/{_source?.StreamName ?? Host}"; }
    }

    public string WebSocketUrl
    {
        get { return $"/ws/{_source.StreamName}"; }
    }
    public ReplicatorVm(VideoStreamReplicator source)
    {
        _source = source;
        _inTransferSpeed = new SpeedVm();
        _outTransferSpeed = new SpeedVm();
    }
    public string InTransferSpeed => _inTransferSpeed.Calculate(Source.StreamMultiplexer.TotalReadBytes);
    public string OutTransferSpeed(ulong bytes) => _outTransferSpeed.Calculate(bytes);

    public VideoStreamReplicator Source
    {
        get => _source;
    }

    public string Started
    {
        get
        {
            var dur = DateTime.Now.Subtract(_source.Started);
            return $"{_source.Started:yyyy.MM.dd HH:mm} ({dur.ToString(@"dd\.hh\:mm\:ss")})";
        }
    }

    public Bytes TotalReadBytes => Source.StreamMultiplexer.TotalReadBytes;

    public bool Equals(ReplicatorVm? other)
    {
        if(ReferenceEquals(this, other)) return true;
        if (other == null) return false;

        return this.Host == other.Host && this.Port == other.Port;
    }
}
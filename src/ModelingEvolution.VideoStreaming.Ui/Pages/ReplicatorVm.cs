using ModelingEvolution.Observable;

namespace ModelingEvolution.VideoStreaming.Ui.Pages;

public class ReplicatorVm : IViewFor<IVideoStreamReplicator>, IEquatable<ReplicatorVm>
{
        
    private readonly SpeedVm _inTransferSpeed;
    private readonly SpeedVm _outTransferSpeed;
    private readonly IVideoStreamReplicator _source;
    public IMultiplexingStats MultiplexingStats => _source.MultiplexingStats;
    public Bytes TotalBytes => MultiplexingStats.TotalReadBytes;
    public string Host => _source.VideoAddress.Host;

    public string Address => _source.VideoAddress.ToString();

    public string ViewerUrl
    {
        get { return $"/viewer/{_source?.VideoAddress.StreamName ?? Host}"; }
    }

    public string WebSocketUrl
    {
        get { return $"/ws/{_source.VideoAddress.StreamName}"; }
    }
    public ReplicatorVm(IVideoStreamReplicator source)
    {
        _source = source;
        _inTransferSpeed = new SpeedVm();
        _outTransferSpeed = new SpeedVm();
    }
    public string InTransferSpeed => _inTransferSpeed.Calculate(Source.MultiplexingStats.TotalReadBytes);
    public string OutTransferSpeed(ulong bytes) => _outTransferSpeed.Calculate(bytes);

    public IVideoStreamReplicator Source
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

    public Bytes TotalReadBytes => Source.MultiplexingStats.TotalReadBytes;

    public bool Equals(ReplicatorVm? other)
    {
        if(ReferenceEquals(this, other)) return true;
        if (other == null) return false;

        return this.Source.VideoAddress.Equals(other.Source.VideoAddress);
    }
}
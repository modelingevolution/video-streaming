using ModelingEvolution.Observable;

namespace ModelingEvolution.VideoStreaming.Ui.Pages
{
    public class ServerVm
    {
        private List<string> _errors = new List<string>();
        public IList<string> Erros => _errors;
        private readonly VideoStreamingServer _server;
        
        public bool IsStartEnabled => _server.State == State.Initialized || _server.State == State.Stopped;
        public bool IsStopEnabled => _server.State == State.Running;

    

        public VideoStreamingServer Server => _server;
        
        public string Started
        {
            get
            {
                var started = _server.Started;
                if (started != null)
                {
                    var dur = DateTime.Now.Subtract(started.Value);
                    return $"{started.Value:yyyy.MM.dd HH:mm} ({dur.ToString(@"dd\.hh\:mm\:ss")})";
                }
                else return "-";
            }
        }
        public ServerVm(VideoStreamingServer server)
        {
            _server = server;
            
            Items = new ObservableCollectionView<ReplicatorVm, IVideoStreamReplicator>(x=>new ReplicatorVm(x), this._server.Streams);
        }
        public async Task Start()
        {
           
            if (_server.State == State.Initialized)
            {
                try
                {
                    await _server.LoadConfig();
                }
                catch(Exception ex)
                {
                    _errors.Add(ex.Message);
                }
            }
            _server.Start();
        }

        public async Task Stop()
        {
            _server.Stop();
        }
        public IObservableCollectionView<ReplicatorVm, IVideoStreamReplicator> Items { get; private set; }
        public string BindAddress => $"{_server.Host}:{_server.Port}";

        public Bytes AllocatedBuffersBytes
        {
            get
            {
                long size = 0;
                try
                {
                    for (int i = 0; i < _server.Streams.Count; i++)
                    {
                        var stream = _server.Streams[i];
                        size += (long)stream.MultiplexingStats.BufferLength;
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    // We don't care.
                }

                return size;
            }
        }

        public string ReconnectStatus
        {
            get
            {
                if (_server.IsReconnecting)
                    return "Reconnecting...";
                return this._server.NxReconnect.Subtract(DateTime.Now).ToString(@"mm\:ss");
            }
        }

        public Task ResetConfig()
        {
            return _server.ServerConfigProvider.Reset();
        }
    }
}

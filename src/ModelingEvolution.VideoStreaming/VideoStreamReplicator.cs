using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using CliWrap;
using FFmpeg.NET;
using MicroPlumberd;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelingEvolution.VideoStreaming.Events;
using ModelingEvolution.VideoStreaming.Nal;

#pragma warning disable CS4014


namespace ModelingEvolution.VideoStreaming
{
    public readonly struct Bytes
    {
        private readonly string _text;
        private readonly long _value;
        private readonly sbyte _precision;
        private Bytes(long value, sbyte precision=1)
        {
            _value = value;
            _text = value.WithSizeSuffix(precision);
            _precision = precision;
        }
        public static implicit operator Bytes(ulong value)
        {
            return new Bytes((long)value);
        }
        public static implicit operator Bytes(long value)
        {
            return new Bytes(value);
        }

        public override string ToString() => _text;
    }
    static class SizeExtensions
    {
        static readonly string[] SizeSuffixes =
            { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

        public static string WithSizeSuffix(this int value, int decimalPlaces = 1)
        {
            return ((long)value).WithSizeSuffix(decimalPlaces);
        }


        public static string WithSizeSuffix(this ulong value, int decimalPlaces = 1)
        {
            if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
            if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag = (int)Math.Log(value, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            // make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return string.Format("{0:n" + decimalPlaces + "} {1}",
                adjustedSize,
                SizeSuffixes[mag]);
        }
        public static string WithSizeSuffix(this long value, int decimalPlaces = 1)
        {
            if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
            if (value < 0) { return "-" + WithSizeSuffix(-value, decimalPlaces); }
            if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag = (int)Math.Log(value, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            // make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return string.Format("{0:n" + decimalPlaces + "} {1}",
                adjustedSize,
                SizeSuffixes[mag]);
        }
    }
    public static class Extensions
    {
        public static async Task WritePrefixedAsciiString(this Stream stream, string value)
        {
            var name = Encoding.ASCII.GetBytes(value);
            stream.WriteByte((byte)name.Length);
            await stream.WriteAsync(name);
        }
    }
    public record Recording(string FileName, string FullPath, string Name, DateTime Started, TimeSpan Duration, Bytes Size)
    {
        private string? _displayName;
        private bool _displayNameLoaded;
        public string DisplayName
        {
            get
            {
                if (_displayNameLoaded)
                    return _displayName;
                string metadata = FullPath + ".name";
                _displayName = File.Exists(metadata) ? File.ReadAllText(metadata) : Name;
                _displayNameLoaded = true;
                return _displayName;
            }
            set
            {
                string metadata = FullPath + ".name";
                File.WriteAllText(metadata, value);
                _displayName = value;
            }
        }
        public void Delete()
        {
            File.Delete(FullPath);
        }
    }

    public class StreamPersister : INotifyPropertyChanged
    {
        record PersisterStream(Stream Stream, ILogger<StreamPersister> logger)
        {
            public CancellationTokenSource GracefullCencellationTokenSource = new();
            public CancellationTokenSource ForcefullCencellationTokenSource = new();

            public async Task Close()
            {
                try
                {
                    await GracefullCencellationTokenSource.CancelAsync();
                    ForcefullCencellationTokenSource.CancelAfter(TimeSpan.FromSeconds(30));
                }
                catch(Exception ex)
                {
                    logger.LogWarning(ex,"Coudn't close nicely ffmpeg.");
                }

                await Stream.DisposeAsync();
            }
        }
        private readonly int _localPort;
        private readonly string _dataDir;
        private readonly string ffmpegExec;
        private readonly Dictionary<VideoAddress, PersisterStream> _streams = new();
       
        public bool IsStartDisabled(VideoAddress address) => _streams.ContainsKey(address);
        public bool IsStopDisabled(VideoAddress address) => !IsStartDisabled(address);
        public bool IsRecording(VideoAddress address) => _streams.ContainsKey(address);
        public IEnumerable<Recording> Files => Directory.Exists(_dataDir) ? Directory.EnumerateFiles(_dataDir,"*.mp4")
            .Select(Parse)
            .Where(x=>x!=null)
            .OrderByDescending(x=>x.Started) : Array.Empty<Recording>();

        const string pattern = @"^(.+?)\.(\d{4})(\d{2})(\d{2})\.((?:\d{1,2}\.)?)(\d{2})(\d{2})(\d{2})-(\d+)\.mp4$";
        static readonly Regex regex = new Regex(pattern, RegexOptions.Compiled);
        private readonly ILogger<StreamPersister> _logger;

        public StreamPersister(VideoStreamingServer srv, IConfiguration configuration, ILogger<StreamPersister> logger)
        {
            _logger = logger;
            _localPort = srv.Port;
            _dataDir = configuration.GetValue<string>("VideoStorageDir");
            ffmpegExec = configuration.GetValue<string>("FFMpeg");
            logger.LogInformation("Blob Storage: " + _dataDir);
            logger.LogInformation("ffmpeg is taken from " + ffmpegExec);
        }

        private static Recording Parse(string fullName)
        {
            var fileName = Path.GetFileName(fullName);
            Match match = regex.Match(fileName);

            if (match.Success)
            {
                string videoName = match.Groups[1].Value;
                string year = match.Groups[2].Value;
                string month = match.Groups[3].Value;
                string day = match.Groups[4].Value;
                string optionalDays = match.Groups[5].Value;
                string hour = match.Groups[6].Value;
                string minute = match.Groups[7].Value;
                string second = match.Groups[8].Value;
                string duration = match.Groups[9].Value;

                DateTime date = new DateTime(int.Parse(year), int.Parse(month), int.Parse(day));
                TimeSpan time = new TimeSpan(int.Parse(hour), int.Parse(minute), int.Parse(second));

                if (!string.IsNullOrEmpty(optionalDays)) 
                    time = time.Add(TimeSpan.FromDays(int.Parse(optionalDays)));
                date += time;
                FileInfo finto = new FileInfo(fullName);
                return new Recording(fileName, fullName,videoName, date, TimeSpan.FromSeconds(int.Parse(duration)), finto.Length);
            }

            return null;
        }
        public async Task Stop(VideoAddress address)
        { 
            _streams[address].Close();
            _streams.Remove(address);
        }
        public async Task Save2(VideoAddress address, HashSet<string> tags)
        {
            var stdOutBuffer = new StringBuilder();
            var stdErrBuffer = new StringBuilder();

            string outputFilePath = null;
            try
            {
                using TcpClient tcpClient = new TcpClient("localhost", _localPort);
                await using NetworkStream h264Stream = tcpClient.GetStream();
                await using BufferedStream bufferedStream = new BufferedStream(h264Stream);

                var persisterStream = new PersisterStream(bufferedStream, _logger);
                _streams.Add(address, persisterStream);
                OnPropertyChanged();

                outputFilePath = await Handshake(address, tags, bufferedStream);

                Debug.WriteLine($"About to save: {outputFilePath}");

                var result = await Cli.Wrap(ffmpegExec)
                    //.WithArgumentsIf(address.Protocol == "mjpeg", $"-i - -c:v libx264 -f mp4 -an -y \"{outputFilePath}\"")
                    .WithArgumentsIf(address.Protocol == "mjpeg", $"-i - -c:v h264 -preset:v ultrafast -f mp4 -an -y \"{outputFilePath}\"")
                    .WithArgumentsIf(address.Protocol != "mjpeg", $"-f h264")
                    .WithStandardInputPipe(PipeSource.FromStream(bufferedStream))
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
                    .ExecuteAsync(persisterStream .ForcefullCencellationTokenSource.Token,
                        persisterStream .GracefullCencellationTokenSource.Token);
               
                Debug.WriteLine(result.ExitCode);
                Debug.WriteLine(stdOutBuffer);
                Debug.WriteLine(stdErrBuffer);

                await CompleteSave(outputFilePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(stdOutBuffer);
                Debug.WriteLine(stdErrBuffer);

                if (outputFilePath != null) await CompleteSave(outputFilePath);
                Debug.WriteLine("Save failed!");
                Debug.WriteLine(ex.Message);
            }
            _streams.Remove(address);
            OnPropertyChanged("Files");
        }

        private async Task CompleteSave(string outputFilePath)
        {
            var ffmpeg = new Engine(ffmpegExec);
            if (!Path.IsPathRooted(outputFilePath))
                outputFilePath = Path.Combine(Directory.GetCurrentDirectory(), outputFilePath);
            if (!File.Exists(outputFilePath)) return;

            var metadata = await ffmpeg.GetMetaDataAsync(new InputFile(outputFilePath), CancellationToken.None);
            if (metadata == null) return;

            var dst = outputFilePath.Replace(".mp4", $"-{(int)metadata.Duration.TotalSeconds}.mp4");
            File.Move(outputFilePath, dst);
        }

        private async Task<string> Handshake(VideoAddress address, HashSet<string> tags, Stream h264Stream)
        {
            StringBuilder outFile = new StringBuilder();
            if (tags.Any())
            {
                var first = tags.First();
                await h264Stream.WritePrefixedAsciiString(first);
                outFile.Append(first);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(address.StreamName))
                {
                    await h264Stream.WritePrefixedAsciiString(address.StreamName);
                    outFile.Append(address.StreamName);
                }
                else
                {
                    await h264Stream.WritePrefixedAsciiString(address.Host);
                    outFile.Append(address.Host);
                }
            }

            var n = DateTime.Now;
            var dateSegment = n.ToString("yyyyMMdd");
            var timeSpan = n.TimeOfDay;

            if (timeSpan.Days > 0)
                outFile.Append($".{dateSegment}.{timeSpan.Days}.{timeSpan.Hours:D2}{timeSpan.Minutes:D2}{timeSpan.Seconds:D2}.mp4");
            else
                outFile.Append($".{dateSegment}.{timeSpan.Hours:D2}{timeSpan.Minutes:D2}{timeSpan.Seconds:D2}.mp4");

            if (!Directory.Exists(_dataDir))
                Directory.CreateDirectory(_dataDir);
            string outputFilePath = Path.Combine(_dataDir, outFile.ToString());
            _logger.LogInformation("ffmpeg is configured to save file at: " + outputFilePath);
            return outputFilePath;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    static class CommandExtension
    {
        public static Command WithArgumentsIf(this Command cmd, bool condition, string command) => condition ? cmd.WithArguments(command) : cmd;
    }
    public class VideoStreamReplicator : IDisposable
    {
        public event EventHandler Stopped;
        private NetworkStream _source;
        private StreamMultiplexer? _multiplexer;
        private readonly string _host;
        private readonly string _protocol;
        private readonly HashSet<string> _tags = new();
        private readonly int _port;
        private readonly string _streamName;
        private readonly ILoggerFactory _loggerFactory;
        private readonly DateTime _started;
        private readonly IPlumber _plumber;
        private TcpClient _client;
        public StreamMultiplexer StreamMultiplexer => _multiplexer;
        public string Host => _host;
        public string Protocol => _protocol;
        public DateTime Started => _started;
        public int Port => _port;
        public string StreamName => _streamName;
        public VideoAddress VideoAddress => new VideoAddress( this.Protocol,Host, Port, StreamName);
        public HashSet<string> Tags => _tags;
        public VideoStreamReplicator(string protocol, string host, int port, string streamName, string[] tags,
            ILoggerFactory loggerFactory, IPlumber plumber)
        {
            _host = host;
            this._port = port;
            _streamName = streamName;
            _loggerFactory = loggerFactory;
            _plumber = plumber;
            _protocol = protocol;
            _started = DateTime.Now;
            foreach (var tag in tags)
                _tags.Add(tag);
            
        }

        public VideoStreamReplicator Connect()
        {
            if (_client != null) throw new InvalidOperationException("Cannot reuse Replicator.");

            _client = new TcpClient(_host, _port);
            try
            {
                _source = _client.GetStream();
                if (!string.IsNullOrWhiteSpace(_streamName))
                {
                    var bytes = Encoding.ASCII.GetBytes(_streamName);
                    _source.WriteByte((byte)bytes.Length);
                    _source.WriteAsync(bytes);
                }
                var streamingStarted = new StreamingStarted { Source = _host };
                
                _plumber.AppendEvent(streamingStarted, _host);
            }
            catch
            { 
                _client.Dispose();
                throw;
            }

            _multiplexer = new StreamMultiplexer(new NonBlockingNetworkStream(_source), _loggerFactory.CreateLogger<StreamMultiplexer>());
            _multiplexer.Stopped += OnStreamingStopped;
            _multiplexer.Disconnected += OnClientDisconnected;
            _multiplexer.Start();
            return this;
        }

        private void OnClientDisconnected(object? sender, EventArgs e)
        {
            var clientDisconnected = new ClientDisconnected
            {
                Source = _host
            };

            _plumber.AppendEvent(clientDisconnected, _host);
        }

        private void OnStreamingStopped(object? sender, EventArgs e)
        {
            Stopped?.Invoke(this, EventArgs.Empty);
            _client?.Dispose();
            _multiplexer!.Stopped -= OnStreamingStopped;
            _multiplexer.Disconnected -= OnClientDisconnected;
        }
        public async Task ReplicateTo(HttpContext ns, string? identifier, CancellationToken token = default)
        {
            var d = new ReverseMjpegDecoder();
            await _multiplexer!.Chase(ns, x => d.Decode(x) == JpegMarker.Start ? 0 : null, identifier, token);
        }
        public async Task ReplicateTo(WebSocket ns, string? identifier)
        {
            IDecoder d = new ReverseDecoder();
            await _multiplexer!.Chase(ns, x => d.Decode(x) == NALType.SPS ? 0 : null, identifier);
        }
        public void ReplicateTo(Stream ns, string? identifier)
        {
            if (this._protocol == "mjpeg")
            {
                var d = new ReverseMjpegDecoder();
                _multiplexer!.Chase(ns, x => d.Decode(x) == JpegMarker.Start ? 0 : null, identifier);
            }
            else
            {
                IDecoder d = new ReverseDecoder();
                _multiplexer!.Chase(ns, x => d.Decode(x) == NALType.SPS ? 0 : null, identifier);
            }
        }

        public void Dispose()
        {
            if(_multiplexer != null)
                _multiplexer.Disconnected -= OnClientDisconnected;
            _client?.Dispose();
        }
    }
    
}

﻿using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Net.Sockets;
using System.Reactive;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using CliWrap;
using Emgu.CV;
using EventPi.Abstractions;
using FFmpeg.NET;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelingEvolution.VideoStreaming.Recordings;

namespace ModelingEvolution.VideoStreaming;


#pragma warning disable CS4014
public class VideoIndexer
{
    private bool _isRunning;
    private DateTime? _lastRun;
    private string _dataDir;
    private string _ffmpegExec;
    private bool _ffmpegReady;
    public VideoIndexer(IWebHostingEnv he, IConfiguration configuration)
    {
        _dataDir = configuration.VideoStorageDir(he.WwwRoot);
        _ffmpegExec = configuration.FfmpegPath();
        if (File.Exists(_ffmpegExec))
            _ffmpegReady = true;
    }

    public bool TryRun()
    {
        if (!_isRunning && (_lastRun == null || _lastRun.Value.AddSeconds(30) < DateTime.Now) && _ffmpegReady)
        {
            _isRunning = true;
            Task.Factory.StartNew(Run);
            return true;
        }
        return false;
    }

    private async Task Run()
    {
        try
        {
            var recordings = Recording.GetMissing(_dataDir);
            foreach (var recording in recordings)
                try
                {
                    if (recording.EndsWith("mp4"))
                        Recording.MakeRecording(recording);

                }
                catch
                {
                }
        }
        catch { }
        _lastRun = DateTime.Now;
        _isRunning = false;
    }
}
public interface IUnmergedRecordingManager
{

    IUnmergedRecordingService? Get(VideoRecordingDevice va);

}
public interface IUnmergedRecordingService
{
    bool ShouldRun { get; }
    VideoAddress Address { get; }
    Task<VideoRecordingIdentifier> Start();
    Task<UnmergedRecordingService.StopResult> Stop();
}


public class PersistedStreamVm : INotifyPropertyChanged
{
    public string Format
    {
        get => _format;
        set => SetField(ref _format, value);
    }

    record StreamStorageVm(Stream Stream, ILogger<PersistedStreamVm> logger)
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
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Coudn't close nicely ffmpeg.");
            }

            await Stream.DisposeAsync();
        }
    }
    private readonly int _localPort;
    private readonly string _dataDir;
    private readonly string _ffmpegExec;
    private readonly Dictionary<VideoAddress, StreamStorageVm> _streams = new();

    public bool IsStartDisabled(VideoAddress address)
    {
        return IsRecording(address);
    }

    public bool IsStopDisabled(VideoAddress address) => !IsStartDisabled(address);
    public bool IsRecording(VideoAddress address)
    {
        return _streams.ContainsKey(address);
    }

    public IEnumerable<Recording> Files
    {
        get
        {
            _indexer.TryRun();
            return Recording.Load(_dataDir);
        }
    }


    private readonly ILogger<PersistedStreamVm> _logger;
    private string _format = "mp4";

    public string DataDir => _dataDir;
    private readonly VideoStreamingServer _server;
    
    private readonly VideoIndexer _indexer;
    
    
    public PersistedStreamVm(VideoStreamingServer srv, IConfiguration configuration,
        ILogger<PersistedStreamVm> logger, IWebHostingEnv he, VideoIndexer indexer)
    {
        _server = srv;
        
        _logger = logger;
        _localPort = srv.Port;
        _dataDir = configuration.VideoStorageDir(he.WwwRoot);
        
        if (!Directory.Exists(_dataDir))
            Directory.CreateDirectory(_dataDir);

        _ffmpegExec = configuration.FfmpegPath();
        if (!File.Exists(_ffmpegExec))
            logger.LogWarning("FFMPEG executable not found at: {ffmpeg}", _ffmpegExec);
        indexer.TryRun();
        _indexer = indexer;
        
    }
    public async Task ConnectToVideoFile(string fileName)
    {
        if (fileName.EndsWith(".mp4", true, null))
        {
            string fullFileName = Path.Combine(_dataDir, fileName);
            string fn = Path.GetFileNameWithoutExtension(fileName);
            await _server.ConnectVideoSource(new VideoAddress(VideoCodec.Mjpeg, streamName: fn, vt: VideoTransport.Shm,
                vs: VideoSource.File, file: fullFileName));
        }
        else throw new NotSupportedException("File format not supported");
    }
    private static Recording Parse(string fullName, Regex regex)
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
            return new Recording(fileName, fullName, videoName, date, TimeSpan.FromSeconds(int.Parse(duration)), finto.Length);
        }

        return null;
    }
    public async Task Stop(VideoAddress address)
    {
      
        
        _streams[address].Close();
        _streams.Remove(address);
    }

    public async Task Save(VideoAddress address)
    {
         await Save(address, new HashSet<string>());
    }
    public async Task Save(VideoAddress address, HashSet<string> tags)
    {
        if (Format == "mp4")
        {
            await SaveMp4(address, tags);
        }
        else
            await SaveMjpeg(address, tags);
    }
    private async Task SaveMjpeg(VideoAddress address, HashSet<string> tags)
    {
        var stdOutBuffer = new StringBuilder();
        var stdErrBuffer = new StringBuilder();

        string outputFilePath = null;
        try
        {
            using TcpClient tcpClient = new TcpClient("localhost", _localPort);
            await using NetworkStream inStream = tcpClient.GetStream();
            await using BufferedStream bufferedStream = new BufferedStream(inStream);

            var persisterStream = new StreamStorageVm(bufferedStream, _logger);
            _streams.Add(address, persisterStream);
            OnPropertyChanged();

            outputFilePath = await Handshake(address, tags, bufferedStream);

            Debug.WriteLine($"About to save: {outputFilePath}");

            await using var fs = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            await bufferedStream.CopyToAsync(fs, persisterStream.GracefullCencellationTokenSource.Token);

            Debug.WriteLine(stdOutBuffer);
            Debug.WriteLine(stdErrBuffer);

            await CompleteSave(outputFilePath, "mjpeg");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(stdOutBuffer);
            Debug.WriteLine(stdErrBuffer);

            if (outputFilePath != null) await CompleteSave(outputFilePath, "mjpeg");
            Debug.WriteLine("Save failed!");
            Debug.WriteLine(ex.Message);
        }
        _streams.Remove(address);
        OnPropertyChanged("Files");
    }
    private async Task SaveMp4(VideoAddress address, HashSet<string> tags)
    {
        var stdOutBuffer = new StringBuilder();
        var stdErrBuffer = new StringBuilder();

        string outputFilePath = null;
        try
        {
            using TcpClient tcpClient = new TcpClient("localhost", _localPort);
            await using NetworkStream h264Stream = tcpClient.GetStream();
            await using BufferedStream bufferedStream = new BufferedStream(h264Stream);

            var persisterStream = new StreamStorageVm(bufferedStream, _logger);
            _streams.Add(address, persisterStream);
            OnPropertyChanged();

            outputFilePath = await Handshake(address, tags, bufferedStream);

            Debug.WriteLine($"About to save: {outputFilePath}");

            var result = await Cli.Wrap(_ffmpegExec)
                //.WithArgumentsIf(address.Protocol == "mjpeg", $"-i - -c:v libx264 -f mp4 -an -y \"{outputFilePath}\"")
                .WithArgumentsIf(address.Codec == VideoCodec.Mjpeg, $"-i - -c:v h264 -preset:v ultrafast -f mp4 -an -y \"{outputFilePath}\"")
                .WithArgumentsIf(address.Codec == VideoCodec.H264, $"-f h264")
                .WithStandardInputPipe(PipeSource.FromStream(bufferedStream))
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
                .ExecuteAsync(persisterStream.ForcefullCencellationTokenSource.Token,
                    persisterStream.GracefullCencellationTokenSource.Token);

            Debug.WriteLine(result.ExitCode);
            Debug.WriteLine(stdOutBuffer);
            Debug.WriteLine(stdErrBuffer);

            await CompleteSave(outputFilePath, "mp4");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(stdOutBuffer);
            Debug.WriteLine(stdErrBuffer);

            if (outputFilePath != null) await CompleteSave(outputFilePath, "mp4");
            Debug.WriteLine("Save failed!");
            Debug.WriteLine(ex.Message);
        }
        _streams.Remove(address);
        OnPropertyChanged("Files");
    }

    private async Task CompleteSave(string outputFilePath, string format)
    {
        if (format == "mp4")
        {
            var ffmpeg = new Engine(_ffmpegExec);
            if (!Path.IsPathRooted(outputFilePath))
                outputFilePath = Path.Combine(Directory.GetCurrentDirectory(), outputFilePath);
            if (!File.Exists(outputFilePath)) return;

            var metadata = await ffmpeg.GetMetaDataAsync(new InputFile(outputFilePath), CancellationToken.None);
            if (metadata == null) return;

            var dst = outputFilePath.Replace($".{format}", $"-{(int)metadata.Duration.TotalSeconds}.{format}");
            File.Move(outputFilePath, dst);
        }
        else
        {
            MjpegDecoder d = new MjpegDecoder();
            int frames = 0;
            using (var fs = File.Open(outputFilePath, FileMode.Open))
            {
                int r = fs.ReadByte();
                do
                {
                    if (d.Decode((byte)r) == JpegMarker.Start)
                        frames += 1;
                    r = fs.ReadByte();
                } while (r != -1);
            }

            var sec = frames / 25;
            var dst = outputFilePath.Replace($".{format}", $"-{(int)sec}.{format}");
            File.Move(outputFilePath, dst);
        }
    }

    private async Task<string> Handshake(VideoAddress address, HashSet<string> tags, Stream inStream)
    {
        StringBuilder outFile = new StringBuilder();
        string extension = Format == "mp4" ? "mp4" : "mjpeg";
        if (tags.Any())
        {
            var first = tags.First();
            await inStream.WritePrefixedAsciiString(first);
            outFile.Append(first);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(address.StreamName))
            {
                await inStream.WritePrefixedAsciiString(address.StreamName);
                outFile.Append(address.StreamName);
            }
            else
            {
                await inStream.WritePrefixedAsciiString(address.Host);
                outFile.Append(address.Host);
            }
        }

        var n = DateTime.Now;
        var dateSegment = n.ToString("yyyyMMdd");
        var timeSpan = n.TimeOfDay;

        if (timeSpan.Days > 0)
            outFile.Append($".{dateSegment}.{timeSpan.Days}.{timeSpan.Hours:D2}{timeSpan.Minutes:D2}{timeSpan.Seconds:D2}.{extension}");
        else
            outFile.Append($".{dateSegment}.{timeSpan.Hours:D2}{timeSpan.Minutes:D2}{timeSpan.Seconds:D2}.{extension}");

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
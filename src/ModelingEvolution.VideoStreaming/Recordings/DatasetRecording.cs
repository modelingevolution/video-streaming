using EventPi.Abstractions;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ModelingEvolution.VideoStreaming.Recordings;

public record DatasetRecording : INotifyPropertyChanged
{
    private string _name;
    private Bytes? _size;
    private PublishState _publishState;
    private DateTime? _publishedDate;
    private string _publishError;

    public DatasetRecording(VideoRecordingIdentifier id, string folder, TimeSpan duration, ulong frameCount)
    {
        Id = id;
        DirectoryFullPath = folder;
        Duration = duration;
        FrameCount = frameCount;
        _name = id.CameraNumber == int.MaxValue ? "External" : id.ToString();
    }

    public DateTime? PublishedDate
    {
        get => _publishedDate;
        set => SetField(ref _publishedDate, value);
    }

    public PublishState PublishState
    {
        get => _publishState;
        set => SetField(ref _publishState, value);
    }

    public string PublishError
    {
        get => _publishError;
        set => SetField(ref _publishError, value);
    }

    public VideoRecordingIdentifier Id { get; init; }
    public string DirectoryFullPath { get; init; }

    

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public DateTimeOffset Started => Id.CreatedTime;
    public TimeSpan Duration { get; init; }
    public ulong FrameCount { get; }

    public Bytes Size
    {
        get
        {
            // Calculate all sum of sizes files in DirectoryFullPath
            if (_size == null)
            {
                if (Directory.Exists(DirectoryFullPath))
                    _size = Directory.GetFiles(DirectoryFullPath, "*", SearchOption.AllDirectories)
                        .Sum(f => new FileInfo(f).Length);
                else _size = -1;
            }

            return _size.Value;
        }
    }

    public string DirectoryName => Path.GetFileName(DirectoryFullPath);

    


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
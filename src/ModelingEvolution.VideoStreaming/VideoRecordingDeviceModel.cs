using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using EventPi.Abstractions;
using MicroPlumberd;
using ModelingEvolution.VideoStreaming.Recordings;

namespace ModelingEvolution.VideoStreaming;

#pragma warning disable CS4014
[EventHandler]
public partial class VideoRecordingDeviceModel
{
    public record State : INotifyPropertyChanged
    {
        private bool _isRecording;
        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsRecording
        {
            get => _isRecording;
            set => SetField(ref _isRecording, value);
        }

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

    public State this[VideoRecordingDevice dev] => _index.GetOrAdd(dev, x => new());
    private readonly ConcurrentDictionary<VideoRecordingDevice, State> _index = new();
    private async Task Given(Metadata m, DatasetRecordingStarted ev)
    {
        this[m.StreamId<VideoRecordingIdentifier>()].IsRecording = true;
    }
    private async Task Given(Metadata m, DatasetRecordingStopped ev)
    {
        this[m.StreamId<VideoRecordingIdentifier>()].IsRecording = false;
    }
}
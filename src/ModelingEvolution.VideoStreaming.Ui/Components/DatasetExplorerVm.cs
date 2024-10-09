using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MicroPlumberd;
using ModelingEvolution.Observable;
using ModelingEvolution.VideoStreaming.Recordings;

namespace ModelingEvolution.VideoStreaming.Ui.Components;

internal class DatasetExplorerVm :INotifyPropertyChanged, IDisposable
{
    private readonly DatasetRecordingsModel _model;
    private readonly ObservableCollectionView<DatasetRecordingVm, DatasetRecording> _items; 
    public DatasetExplorerVm(DatasetRecordingsModel model, ICommandBus bus)
    {
        _model = model;
        _items = new ObservableCollectionView<DatasetRecordingVm, DatasetRecording>(x => new(x,bus,this), _model.Items);
        _items.CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RaiseChange();

    public IReadOnlyList<DatasetRecordingVm> Items => _items;

        
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


    public void RaiseChange() => OnPropertyChanged(string.Empty);

    public void Dispose()
    {

        _items.Dispose();
        foreach (var i in _items)
            i.Dispose();
    }
}
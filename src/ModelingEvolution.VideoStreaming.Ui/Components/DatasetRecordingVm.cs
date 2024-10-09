using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Ocl;
using EventPi.Abstractions;
using Google.Protobuf.WellKnownTypes;
using MicroPlumberd;
using Microsoft.Extensions.Configuration;
using ModelingEvolution.Observable;
using ModelingEvolution.VideoStreaming.Recordings;

namespace ModelingEvolution.VideoStreaming.Ui.Components
{
    class DatasetRecordingVm : IViewFor<DatasetRecording>, IEquatable<DatasetRecordingVm>, IDisposable
    {
        private readonly ICommandBus _bus;
        private readonly DatasetExplorerVm _parent;
        private string _name;

        public DatasetRecordingVm(DatasetRecording source, ICommandBus bus, DatasetExplorerVm parent)
        {
            _bus = bus;
            _parent = parent;
            _name = source.Name;
            Source = source;
            source.PropertyChanged += OnSourceChanged;
        }

        private void OnSourceChanged(object? sender, PropertyChangedEventArgs e)
        {
            _name = Source.Name;
            _parent.RaiseChange();
        }

        public DatasetRecording Source { get; }
        public string InspectUrl => $"/inspect/{Id}";
        public string UploadUrl => $"/upload-dataset/{Id}";
        public async Task Delete()
        {
            await _bus.SendAsync(Source.Id, new DeleteDatasetRecording());
            //File.Delete(DirectoryFullPath);
        }
        public Guid Id => Source.Id;
        public string Name
        {
            get => _name;
            set
            {
                if (_name == value) return;
                _name = value;
                _bus.SendAsync(Source.Id, new RenameDatasetRecording() { Name = value });
            }
        }

        public bool Equals(DatasetRecordingVm? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Source.Equals(other.Source);
        }

        public void Dispose()
        {
            Source.PropertyChanged -= OnSourceChanged;
        }
    }





}

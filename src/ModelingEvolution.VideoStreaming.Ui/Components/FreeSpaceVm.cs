using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModelingEvolution.VideoStreaming.Ui.Components
{
    static class Extensions {
        public static int IndexOf<T>(this IEnumerable<T> source, Func<T,bool> predicate){
            int index = 0;
            foreach (T item in source){
                if (predicate(item))
                    return index;
                index++;
            }
            return -1;
        }
    }
    class FreeSpaceVmProvider : IDisposable, IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, Vm> _index = new();
        private readonly Timer t;
        public Vm Get(string path) => _index.GetOrAdd(path, x => new Vm(x));

        public FreeSpaceVmProvider()
        {
            this.t = new Timer(OnCheck, null, 0, 5000);
        }

        private void OnCheck(object? state)
        {
            foreach (var i in _index.Values)
            {
                //DirectoryInfo info = new DirectoryInfo(i.Path);
                var driveInfo = new DriveInfo(i.Path);
                Bytes freeSpace = driveInfo.AvailableFreeSpace;
                i.FreeSpace = freeSpace.ToString();
            }
        }

        public void Dispose()
        {
            t.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await t.DisposeAsync();
        }
        internal class Vm(string path) : INotifyPropertyChanged
        {
            public string Path => path;
            private string _freeSpace = string.Empty;
            public event PropertyChangedEventHandler? PropertyChanged;

            public string FreeSpace
            {
                get => _freeSpace;
                set => SetField(ref _freeSpace, value);
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
    }
    
}

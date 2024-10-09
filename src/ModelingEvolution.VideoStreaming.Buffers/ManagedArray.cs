using System.Buffers;
using System.Collections;

namespace ModelingEvolution.VideoStreaming.Buffers;

/// <summary>
/// This collection uses ArrayPool for memory menagement.
/// </summary>
/// <typeparam name="T"></typeparam>
public class ManagedArray<T> : ICollection<T>, IDisposable
{
    
    private T[] _buffer;
    private int _size = 0;
    private bool _disposed;
    public T this[int index]
    {
        get => _buffer[index];
        set
        {
            if (index >= _size)
            {
                var nb = ArrayPool<T>.Shared.Rent(index + 1);
                ALLOCATED_BYTES += nb.Length;
                Array.Copy(_buffer, 0, nb, 0, _size);
                if (_buffer.Length > 0)
                {
                    ArrayPool<T>.Shared.Return(_buffer);
                    ALLOCATED_BYTES -= _buffer.Length;
                }
                _buffer = nb;
                _size = index + 1;
            }
            _buffer[index] = value;
        }
    }

    ~ManagedArray()
    {
        Dispose(false);
    }
    public ManagedArray()
    {
        _buffer = Array.Empty<T>();
    }

    public T[] GetBuffer() => _buffer;
    public ManagedArray(int capacity)
    {
        _buffer = capacity > 0 ? ArrayPool<T>.Shared.Rent(capacity) : Array.Empty<T>();
        ALLOCATED_BYTES += _buffer.Length;
    }
    public ManagedArray(params T[] array)
    {
        _buffer = ArrayPool<T>.Shared.Rent(array.Length);
        ALLOCATED_BYTES += _buffer.Length;
        Array.Copy(array, 0, _buffer, 0, array.Length);
        _size = array.Length;
    }

    public void Add(T item)
    {
        if (_size == _buffer.Length)
        {
            var na = ArrayPool<T>.Shared.Rent(Math.Max(_size * 2, 8));
            ALLOCATED_BYTES += na.Length;
            Array.Copy(_buffer, 0, na, 0, _size);
            if (_buffer.Length > 0)
            {
                ArrayPool<T>.Shared.Return(_buffer);
                ALLOCATED_BYTES -= _buffer.Length;
            }
            _buffer = na;
        }

        _buffer[_size++] = item;
    }

    public void Clear()
    {
        if (_buffer.Length > 0)
        {
            ArrayPool<T>.Shared.Return(_buffer);
            ALLOCATED_BYTES -= _buffer.Length;
            _buffer = Array.Empty<T>();
        }

        _size = 0;
    }

    public static long ALLOCATED_BYTES = 0;
    public bool Contains(T item)
    {
        for (var i = 0; i < _size; i++)
            if (EqualityComparer<T>.Default.Equals(_buffer[i], item))
                return true;

        return false;
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < _size)
            throw new ArgumentException(
                "The number of elements in the source ManagedArray is greater than the available space from arrayIndex to the end of the destination array.");

        Array.Copy(_buffer, 0, array, arrayIndex, _size);
    }

    public bool Remove(T item)
    {
        for (var i = 0; i < _size; i++)
            if (EqualityComparer<T>.Default.Equals(_buffer[i], item))
            {
                _size--;
                if (i < _size) Array.Copy(_buffer, i + 1, _buffer, i, _size - i);

                _buffer[_size] = default;
                return true;
            }

        return false;
    }

    public int Count => _size;
    public bool IsReadOnly { get; }

    public IEnumerator<T> GetEnumerator()
    {
        for (var i = 0; i < _size; i++) yield return _buffer[i];
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private void Dispose(bool disposing)
    {
        if (_buffer.Length > 0 && !_disposed)
        {
            ArrayPool<T>.Shared.Return(_buffer);
            ALLOCATED_BYTES -= _buffer.Length;
        }
        _disposed = true;
    }
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
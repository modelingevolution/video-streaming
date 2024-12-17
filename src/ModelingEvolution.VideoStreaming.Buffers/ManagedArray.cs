using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;

namespace ModelingEvolution.VideoStreaming.Buffers;

/// <summary>
/// This collection uses ArrayPool for memory menagement.
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class ManagedArray<T> : ICollection<T>, IDisposable, IList<T>, IReadOnlyList<T>
{
    private T[] _buffer;
    private int _size = 0;
    private bool _disposed;
    

    public ManagedArray()
    {
        _buffer = Array.Empty<T>();
    }

    public ManagedArray(int capacity)
    {
        _buffer = capacity > 0 ? ArrayPool<T>.Shared.Rent(capacity) : Array.Empty<T>();
        Interlocked.Add(ref ALLOCATED_BYTES, _buffer.Length);
    }

    public ManagedArray(params T[] array)
    {
        _buffer = ArrayPool<T>.Shared.Rent(array.Length);
        Interlocked.Add(ref ALLOCATED_BYTES, _buffer.Length);
        Array.Copy(array, 0, _buffer, 0, array.Length);
        _size = array.Length;
    }

    ~ManagedArray()
    {
        Dispose(false);
    }

    public T this[int index]
    {
        get
        {
            if (index >= _size || index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            return _buffer[index];
        }
        set
        {
            EnsureCapacity(index + 1);
            _buffer[index] = value;
            if (index >= _size) _size = index + 1;
        }
    }

    public int Count => _size;
    public bool IsReadOnly => false;

    public void Add(T item)
    {
        EnsureCapacity(_size + 1);
        _buffer[_size++] = item;
    }

    public void Clear() => Clear(false);
    public void Clear(bool disposeMemory)
    {
        if (_buffer.Length > 0 && disposeMemory)
        {
            ArrayPool<T>.Shared.Return(_buffer);
            Interlocked.Add(ref ALLOCATED_BYTES, -_buffer.Length);
            _buffer = Array.Empty<T>();
        }
        _size = 0;
    }

    public bool Contains(T item) => IndexOf(item) != -1;

    public void CopyTo(T[] array, int arrayIndex)
    {
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0 || arrayIndex + _size > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        Array.Copy(_buffer, 0, array, arrayIndex, _size);
    }

    public bool Remove(T item)
    {
        int index = IndexOf(item);
        if (index < 0) return false;
        RemoveAt(index);
        return true;
    }
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public int IndexOf(T item)
    {
        for (int i = 0; i < _size; i++)
            if (EqualityComparer<T>.Default.Equals(_buffer[i], item))
                return i;
        return -1;
    }

    public void Insert(int index, T item)
    {
        if (index < 0 || index > _size) throw new ArgumentOutOfRangeException(nameof(index));
        EnsureCapacity(_size + 1);
        Array.Copy(_buffer, index, _buffer, index + 1, _size - index);
        _buffer[index] = item;
        _size++;
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _size) throw new ArgumentOutOfRangeException(nameof(index));
        Array.Copy(_buffer, index + 1, _buffer, index, _size - index - 1);
        _buffer[--_size] = default;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < _size; i++) yield return _buffer[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void EnsureCapacity(int minCapacity)
    {
        if (minCapacity <= _buffer.Length) return;
        int newCapacity = Math.Max(minCapacity, _size * 2);
        T[] newBuffer = ArrayPool<T>.Shared.Rent(newCapacity);
        Interlocked.Add(ref ALLOCATED_BYTES, newBuffer.Length);
        Array.Copy(_buffer, 0, newBuffer, 0, _size);

        if (_buffer.Length > 0)
        {
            ArrayPool<T>.Shared.Return(_buffer);
            Interlocked.Add(ref ALLOCATED_BYTES, -_buffer.Length);
        }

        _buffer = newBuffer;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (_buffer.Length > 0)
        {
            ArrayPool<T>.Shared.Return(_buffer);
            Interlocked.Add(ref ALLOCATED_BYTES, -_buffer.Length);
        }
        _disposed = true;
    }

    public static long ALLOCATED_BYTES = 0;  // Memory allocation tracking

    public T[] GetBuffer()
    {
        return this._buffer;
    }
}

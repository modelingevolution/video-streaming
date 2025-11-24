using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Emgu.CV;
using Emgu.CV.Util;
using Emgu.CV.CvEnum;
using Emgu.CV.Shape;

using System.Drawing;
using ModelingEvolution.VideoStreaming.Nal;
using System;

namespace ModelingEvolution.VideoStreaming;
public class CyclicMemoryBuffer
{
    private readonly uint _maxObjectSize;
    private readonly Memory<byte> _buffer;
    private readonly MemoryHandle _handle;
    private uint _cursor;
    private ulong _cycle;
    private ulong _written;
    public long Size => _buffer.Length;
    public ulong MaxObjectSize => _maxObjectSize;
    public CyclicMemoryBuffer(uint capacity, uint maxObjectSize)
    {
        _buffer = new Memory<byte>(new byte[capacity * maxObjectSize]);
        _buffer.Span.Fill(0xFF);
        _handle = _buffer.Pin();
        _maxObjectSize = maxObjectSize;
    }
    public void Write<T>(in T data, uint offset = 0) where T : struct
    {
        MemoryMarshal.Write(_buffer.Span.Slice((int)(_cursor + offset)), in data);
    }
    public ulong SpaceLeft => (ulong)(_buffer.Length - _cursor);
    public unsafe byte* GetPtr(int offset = 0)
    {
        return ((byte*)_handle.Pointer) + _cursor + offset;
    }
    public Memory<byte> Use(uint size)
    {
        var u = _buffer.Slice((int)_cursor, (int)size);
        Advance(size);
        return u;
    }
    /// <summary>
    /// Advances the specified size.
    /// </summary>
    /// <param name="size">The size.</param>
    /// <returns>true if it is the next cycle in the buffer.</returns>
    public bool Advance(uint size)
    {
        bool nextIteration = false;
        var left = _buffer.Length - _cursor;
        if (left <= (_maxObjectSize))
        {
            _cursor = 0;
            _written += (ulong)size;
            nextIteration = true;
            Interlocked.Increment(ref _cycle);
            Debug.WriteLine($"Buffer is full for writing, resetting. Last frame was: {_cycle - 1} ");
        }
        else
        {
            _cursor += size;
            _written += (ulong)size;
        }
        return nextIteration;
    }
}
using System.Runtime.CompilerServices;

namespace ModelingEvolution.VideoStreaming.Buffers;

public sealed class CyclicArrayBuffer
{
    public int Offset;
    public int Ready;
    public readonly byte[] Data;

    public int Size
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Data.Length;
    }

    public Memory<byte> UnusedData
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Data.AsMemory(Offset);
    }

    public int Free
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Size - Offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AdvanceReadBy(int read)
    {
        Ready += read;
        Offset += read;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<byte> Use(int b)
    {
        if (b > Ready) throw new InvalidOperationException();

        var m = Data.AsMemory(Offset - Ready, b);
        Ready -= b;
        return m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DefragmentTail()
    {
        if (Ready == 0)
        {
            Offset = 0;
            return;
        }
        var fragment = Data.AsMemory(Offset - Ready, Ready);
        fragment.CopyTo(Data.AsMemory(0, fragment.Length));
        Offset = fragment.Length;
    }

    public CyclicArrayBuffer(int size)
    {
        Data = new byte[size];
    }
}
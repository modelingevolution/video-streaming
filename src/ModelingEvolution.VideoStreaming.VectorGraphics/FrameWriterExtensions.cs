using System.Buffers;
using System.Runtime.InteropServices;

namespace ModelingEvolution.VideoStreaming.VectorGraphics;

public static class FrameWriterExtensions
{
    public static MemoryBufferWriter GetWriter(this ref Memory<byte> mem)
    {
        return new MemoryBufferWriter(ref mem);
    }
    public static void WriteFrameNumber(this IBufferWriter<byte> buffer, ulong frame)
    {
        MemoryMarshal.Write(buffer.GetSpan(sizeof(ulong)), frame);
        buffer.Advance(sizeof(long));
    }

    public static void WriteFrameEnd(this IBufferWriter<byte> buffer)
    {
        var destination = buffer.GetSpan(SubHeaderSize).Slice(0,SubHeaderSize);
        MemoryMarshal.Write( destination,ProtoStreamClient.EOF);
        buffer.Advance(SubHeaderSize);
    }
    private static readonly int SubHeaderSize = Marshal.SizeOf<ProtoStreamClient.SubHeader>();
    public static void WriteFramePayload<T>(this IBufferWriter<byte> buffer, ushort type, T value)
    {
        var header = buffer.GetSpan(SubHeaderSize)
            .Slice(0,SubHeaderSize);

        var msgMem = buffer.GetMemory(0).Slice(SubHeaderSize);
        var msgWriter = msgMem.GetWriter();
        ProtoBuf.Serializer.Serialize(msgWriter, value);
            
        var h = new ProtoStreamClient.SubHeader((uint)msgWriter.WrittenMemory, type);
        MemoryMarshal.Write(header,h);
        buffer.Advance(msgWriter.WrittenMemory + SubHeaderSize);
    }
        
}
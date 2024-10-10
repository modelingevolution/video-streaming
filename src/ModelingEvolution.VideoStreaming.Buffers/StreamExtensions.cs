using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace ModelingEvolution.VideoStreaming.Buffers;

public static class StreamExtensions
{
    private static readonly int HEADER_SIZE = Marshal.SizeOf<FrameMetadata>();

    public static async Task WriteAsync<T>(this Stream stream, T data) where T : struct
    {
        using var tmp = MemoryPool<byte>.Shared.Rent(Marshal.SizeOf<T>());
        MemoryMarshal.Write(tmp.Memory.Span, in data);
        await stream.WriteAsync(tmp.Memory);
    }
    
    public static async Task Copy2(this Stream stream, ConcurrentQueue<Memory<byte>> queue,
        int bufferSize = 16 * 1024 * 1024, bool throwOnEnd = true, CancellationToken token = default)
    {
        CyclicArrayBuffer b = new CyclicArrayBuffer(bufferSize);

        while (!token.IsCancellationRequested)
        {
            await stream.ReadIfRequired(b, HEADER_SIZE, throwOnEnd,token);
            // We have enough to read the header
            var m = MemoryMarshal.AsRef<FrameMetadata>(b.Use(HEADER_SIZE).Span);
            if (!m.IsOk)
                throw new InvalidOperationException("Memory is corrupt or was overriden.");

            await stream.ReadIfRequired(b, (int)m.FrameSize, throwOnEnd, token);
            var frame = b.Use((int)m.FrameSize);
            queue.Enqueue(frame);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task ReadIfRequired(this Stream stream, CyclicArrayBuffer buffer, int required,
        bool throwOnEnd,
        CancellationToken token)
    {
        if (buffer.Ready >= required)
            return;

        int minToRead = required - buffer.Ready;
        if (buffer.Free < minToRead)
            buffer.DefragmentTail();

        int read = 0;
        do
        {
            var r = await stream.ReadAtLeastAsync(buffer.UnusedData, minToRead, throwOnEnd, token);
            buffer.AdvanceReadBy(r);
            read += r;
            if (read < minToRead)
                //Thread.Yield();
                await Task.Delay(1, token);
        } 
        while (read < minToRead);

    }


}
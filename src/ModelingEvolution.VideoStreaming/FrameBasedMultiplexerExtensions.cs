using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace ModelingEvolution.VideoStreaming;

#pragma warning disable CS4014
public static class FrameBasedMultiplexerExtensions
{
    public static async IAsyncEnumerable<Frame> Read(this IBufferedFrameMultiplexer b, 
        int fps=30, ILogger logger = null,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        TimeSpan w = TimeSpan.FromSeconds(1d / (fps+fps));
        int offset = b.LastFrameOffset;
        var buffer = b.Buffer();
        while(b.ReadFrameCount == 0)
            await Task.Delay(w+w, token);

        ulong lastFrame = 0;
        while (!token.IsCancellationRequested)
        {
            if (b.IsEnd(offset))// buffer.Length - offset <= b.Padding)
            {
                offset = 0;
                Debug.WriteLine($"Buffer is full for reading, resetting.");
            }
            var metadata = buffer.ReadMetadata(offset);
            if (lastFrame == 0) lastFrame = metadata.FrameNumber;
            if (metadata.FrameNumber != lastFrame++)
            {
                // Frame is not in order. Resetting.
                var expecting = lastFrame - 1;
                var read = metadata.FrameNumber;
                var prvOffset = offset;
                offset = b.LastFrameOffset;
                metadata = buffer.ReadMetadata(offset);
                lastFrame = metadata.FrameNumber+1;
                logger?.LogWarning($"Frame not in order, resetting stream. Expecting: {expecting} " +
                                   $"received {read} from {prvOffset}, resetting to {metadata.FrameNumber} at {offset}");
            }

            if (!metadata.IsOk)
                throw new InvalidOperationException("Memory is corrupt or was overriden.");

            var pendingFrames = (int)(b.ReadFrameCount - metadata.FrameNumber);
            var pendingBytes = (int)(b.TotalReadBytes - metadata.StreamPosition);
            Frame f = new Frame(ref metadata, 
                buffer.Slice(offset+ METADATA_SIZE, (int)metadata.FrameSize), 
                pendingFrames,
                pendingBytes);

            yield return f;

            offset += (int)metadata.FrameSize + METADATA_SIZE;
            while(metadata.FrameNumber == b.ReadFrameCount-1)
                await Task.Delay(w, token); // might be spinwait?
        }
    }
    private static readonly int METADATA_SIZE = Marshal.SizeOf<FrameMetadata>();
    public static ref FrameMetadata ReadMetadata(this Memory<byte> buffer, int offset)
    {
        var frameMetadataSpan = buffer.Span.Slice(offset, METADATA_SIZE);
        return ref MemoryMarshal.AsRef<FrameMetadata>(frameMetadataSpan);
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct FrameMetadata(ulong frameNumber, ulong frameSize, ulong streamPosition)
{
    public readonly ulong FrameNumber = frameNumber;
    public readonly ulong FrameSize = frameSize;
    public readonly ulong StreamPosition = streamPosition;
    private readonly ulong _xor = frameNumber ^ frameSize ^ streamPosition;

    public bool IsOk => FrameSize > 0 && _xor == (FrameNumber ^ FrameSize ^ StreamPosition);
}

public readonly struct Frame(ref FrameMetadata metadata, Memory<byte> data, 
    int pendingFrames, 
    int pendingBytes)
{
    public readonly Memory<byte> Data = data;
    public readonly FrameMetadata Metadata = metadata;
    public readonly int PendingFrames = pendingFrames;
    public readonly int PendingBytes = pendingBytes;
}
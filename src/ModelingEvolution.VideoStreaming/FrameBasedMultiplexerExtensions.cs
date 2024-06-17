using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ModelingEvolution.VideoStreaming;

#pragma warning disable CS4014
public static class FrameBasedMultiplexerExtensions
{
    public static async IAsyncEnumerable<Frame> Read(this IBufferedFrameMultiplexer b, 
        int fps=30, 
        [EnumeratorCancellation] CancellationToken token = default)
    {
        TimeSpan w = TimeSpan.FromSeconds(1d / (fps+fps));
        int offset = b.LastFrameOffset;
        var buffer = b.Buffer();
        while(b.ReadFrameCount == 0)
            await Task.Delay(w+w, token);
        
        while (!token.IsCancellationRequested)
        {
            var metadata = buffer.ReadMetadata(offset);
            var pendingFrames = (int)(b.ReadFrameCount - metadata.FrameNumber);
            var pendingBytes = (int)(b.TotalReadBytes - metadata.StreamPosition);
            Frame f = new Frame(ref metadata, 
                buffer.Slice(offset), 
                pendingFrames,
                pendingBytes);

            yield return f;

            offset += (int)metadata.FrameSize + METADATA_SIZE;
            if(buffer.Length - offset < b.Padding)
                offset = 0;

            while(metadata.FrameNumber == b.ReadFrameCount)
                await Task.Delay(w, token);
        }
    }
    private static readonly int METADATA_SIZE = Marshal.SizeOf<FrameMetadata>();
    public static ref FrameMetadata ReadMetadata(this Memory<byte> buffer, int offset)
    {
        var frameMetadataSpan = buffer.Span.Slice(offset, METADATA_SIZE);
        return ref MemoryMarshal.AsRef<FrameMetadata>(frameMetadataSpan);
    }
}

public readonly struct FrameMetadata(ulong frameNumber, ulong frameSize, ulong streamPosition)
{
    public readonly ulong FrameNumber = frameNumber;
    public readonly ulong FrameSize = frameSize;
    public readonly ulong StreamPosition = streamPosition;
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
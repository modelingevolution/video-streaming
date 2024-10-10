using System.Runtime.InteropServices;
using ModelingEvolution.VideoStreaming.Buffers;
using ModelingEvolution.VideoStreaming.Player;

namespace ModelingEvolution.VideoStreaming.OpenVidCam;

public static class Extensions
{
        
    private static readonly int HEADER_SIZE = Marshal.SizeOf<FrameMetadata>();

    public static async Task StartCopyAsync(this Stream stream, Action<JpegFrame> onFrame,
        int bufferSize = 16 * 1024 * 1024, bool throwOnEnd = true, CancellationToken token = default)
    {
        // Runs Copy in a separate long running thread.
        _ = Task.Factory.StartNew(async x =>
        {
            await stream.CopyAsync(onFrame, bufferSize, throwOnEnd, token);
        }, TaskContinuationOptions.LongRunning, token);
    }

    public static async Task CopyAsync(this Stream stream, Action<JpegFrame> onFrame,
        int bufferSize = 16 * 1024 * 1024, bool throwOnEnd = true, CancellationToken token = default)
    {
        CyclicArrayBuffer b = new CyclicArrayBuffer(bufferSize);

        while (!token.IsCancellationRequested)
        {
            await stream.ReadIfRequired(b, HEADER_SIZE, throwOnEnd, token);
            // We have enough to read the header
            var m = MemoryMarshal.AsRef<FrameMetadata>(b.Use(HEADER_SIZE).Span);
            if (!m.IsOk)
                throw new InvalidOperationException("Memory is corrupt or was overriden.");

            await stream.ReadIfRequired(b, (int)m.FrameSize, throwOnEnd, token);
            var frame = b.Use((int)m.FrameSize);
            onFrame(new JpegFrame(m.FrameNumber, frame));
        }
    }
}
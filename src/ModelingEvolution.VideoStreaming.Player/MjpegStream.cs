using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;


namespace ModelingEvolution.VideoStreaming.Player;

public static class Ext{
    private static bool IsEndMemoryStream(Stream stream, int read)
    {
        MemoryStream fs = (MemoryStream)stream;
        if (fs.Position == fs.Length)
            return true;
        return false;
    }

    private static bool IsEndNetworkStream(Stream stream, int read)
    {
        NetworkStream fs = (NetworkStream)stream;
        return fs.Socket.Poll(1, SelectMode.SelectRead) && fs.Socket.Available == 0;
    }

    private static bool IsEndStream(Stream stream, int read)
    {
        return read == 0;
    }
    public static async Task Copy(this Stream stream, ConcurrentQueue<JpegFrame> queue, int bufferSize = 16 * 1024 * 1024, CancellationToken token = default)
    {
        var decoder = new MjpegDecoder();

        byte[] buffer = new byte[bufferSize];
        byte[] buffer2 = new byte[bufferSize];
        byte[] current = buffer;
        Func<Stream, int, bool> isEnd = stream switch
        {
            MemoryStream => IsEndMemoryStream,
            NetworkStream => IsEndNetworkStream,
            _ => IsEndStream
        };
        Memory<byte> tail = Memory<byte>.Empty;
        while (!token.IsCancellationRequested)
        {
            var b = await stream.ReadAsync(current, token);
            var end = isEnd(stream, b);
            if (end && b == 0) return;

            int startOffset = 0;

            for (int i = 0; i < b; i++)
            {
                var marker = decoder.Decode(current[i]);
                if (marker == JpegMarker.End)
                {
                    if (token.IsCancellationRequested) return;

                    var size = i + tail.Length - startOffset + 1;
                    byte[] sharedByteArray = ArrayPool<byte>.Shared.Rent(size);
                    tail.CopyTo(sharedByteArray);
                    Buffer.BlockCopy(current, startOffset, sharedByteArray, tail.Length, i - startOffset + 1);

                    var lastFrame = new JpegFrame() { Data = sharedByteArray, Length = size };
                    queue.Enqueue(lastFrame);
                    startOffset = i + 1;
                    tail = Memory<byte>.Empty;
                }
            }

            var left = b - startOffset;
            tail = left > 0 ? current.AsMemory(startOffset, left) : Memory<byte>.Empty;
            current = current.Swap(buffer, buffer2);
        }
    }
}
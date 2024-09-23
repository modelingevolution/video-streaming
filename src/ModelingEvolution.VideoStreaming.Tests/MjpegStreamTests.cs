using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using FluentAssertions;
using ModelingEvolution.VideoStreaming.Buffers;

namespace ModelingEvolution.VideoStreaming.Tests;

public class MjpegStreamTests
{
    [Fact]
    public async Task FullHeaderAndPayload()
    {
        MemoryStream ms = new MemoryStream();
        var frameMetadata = new FrameMetadata(0, 128, 0);
        await ms.WriteAsync<FrameMetadata>(frameMetadata);
        await ms.WriteAsync(GetBytes(128));
        ms.Position = 0;

        ConcurrentQueue<Memory<byte>> queue = new();
        var action = async () => await ms.Copy2(queue, 128 + Marshal.SizeOf<FrameMetadata>());
        await action.Should().ThrowAsync<Exception>();
        queue.Count.Should().Be(1);

    }
    private byte[] GetBytes(byte max)
    {
        var payload = new byte[max];
        for(byte i = 0; i < max; i++) 
            payload[i] = i;

        return payload;
    }

    [Fact]
    public async Task FragmentedHeader()
    {

    }
    [Fact]
    public async Task FragmentedPayload()
    {
        MemoryStream ms = new MemoryStream();
        var frameMetadata = new FrameMetadata(0, 128, 0);
        await ms.WriteAsync<FrameMetadata>(frameMetadata);
        await ms.WriteAsync(GetBytes(127));
        ms.Position = 0;

        ConcurrentQueue<Memory<byte>> queue = new();
        CancellationTokenSource s = new ();
        _ = ms.Copy2(queue, 128 + Marshal.SizeOf<FrameMetadata>(), false, s.Token);
        await Task.Delay(10);
        queue.Count.Should().Be(0);
        ms.Position -= 1;
        await Task.Delay(10);
        queue.Count.Should().Be(1);
        await s.CancelAsync();
    }
}
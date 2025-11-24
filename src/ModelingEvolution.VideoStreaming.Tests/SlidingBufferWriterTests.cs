using System.Buffers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using ModelingEvolution.VideoStreaming.VectorGraphics;

namespace ModelingEvolution.VideoStreaming.Tests;

static class Ext
{
    public static async IAsyncEnumerable<T> BreakOnCancel<T>(this IAsyncEnumerable<T> items, CancellationToken token =default)
    {
        var it = items.GetAsyncEnumerator(token);
        while(!token.IsCancellationRequested)
        {
            T tmp = default;
            try
            {
                if (await it.MoveNextAsync())
                    tmp = it.Current;

            }
            catch(OperationCanceledException)
            {
                break;
            }

            yield return tmp;
        }
    }
}
public class ProtoStreamClientTests
{
    private CancellationToken CreateWithCancelAfter(TimeSpan ts)
    {
        CancellationTokenSource cts = new CancellationTokenSource();
        cts.CancelAfter(ts);
        return cts.Token;
    }
    
    [Fact]
    public async Task WriteHeaderRead_ShouldTransferData()
    {
        ArrayBufferWriter<byte> buffer = new();
        buffer.WriteFrameNumber(1,2);
        buffer.WriteFrameEnd(2);
        
        var sut = new ProtoStreamClient(NSubstitute.Substitute.For<ISerializer>(),
            NSubstitute.Substitute.For<ILogger<ProtoStreamClient>>());

        
        sut.ProcessFragment(buffer.WrittenSpan);
        
        
        var frames = await sut.Read(CreateWithCancelAfter(TimeSpan.FromSeconds(1)))
            .BreakOnCancel()
            .ToArrayAsync();

        frames.Should().HaveCount(1);
        frames[0].LayerId.Should().Be(2);
        frames[0].Number.Should().Be(1);
        frames[0].Count.Should().Be(0);
    }
    [Fact]
    public async Task WriteFrameWithPayload_ShouldTransferData()
    {
        ArrayBufferWriter<byte> buffer = new(4*1024);
        buffer.WriteFrameNumber(1, 2);
        var vObj = new Text() { Content = "Hello"};
        buffer.WriteFramePayload(1,2,vObj);
        buffer.WriteFrameEnd(2);

        var b = new MessageRegisterBuilder().With(1, typeof(Text)).Build();
        
        var sut = new ProtoStreamClient(new Serializer(b),
            NSubstitute.Substitute.For<ILogger<ProtoStreamClient>>());

        sut.ProcessFragment(buffer.WrittenSpan);

        var frames = await sut.Read(CreateWithCancelAfter(TimeSpan.FromSeconds(1)))
            .BreakOnCancel()
            .ToArrayAsync();

        frames.Should().HaveCount(1);
        frames[0].LayerId.Should().Be(2);
        frames[0].Number.Should().Be(1);
        frames[0].Count.Should().Be(1);
        frames[0].Objects[0].Should().BeEquivalentTo(vObj);
    }
}
public class SlidingBufferWriterTests
{
    [Fact]
    public void Write_ShouldCopyDataToBuffer()
    {
        // Arrange
        var writer = new SlidingBufferWriter();
        byte[] data = new byte[] { 1, 2, 3, 4 };
        // Act
        writer.Write(data, 0, data.Length);
        writer.Advance(data.Length);
        // Assert
        Assert.Equal(data.Length, writer.WrittenBytes);
        Assert.Equal(data, writer.WrittenMemory.ToArray());
    }
    [Fact]
    public void Advance_ShouldIncreaseWrittenBytes()
    {
        // Arrange
        var writer = new SlidingBufferWriter();
        int initialWrittenBytes = writer.WrittenBytes;
        // Act
        writer.Advance(10);
        // Assert
        Assert.Equal(initialWrittenBytes + 10, writer.WrittenBytes);
    }
    [Fact]
    public void GetMemory_ShouldReturnMemoryWithSizeHint()
    {
        // Arrange
        var writer = new SlidingBufferWriter();
        int sizeHint = 1024;
        // Act
        var memory = writer.GetMemory(sizeHint);
        // Assert
        Assert.True(memory.Length >= sizeHint);
    }
    [Fact]
    public void GetSpan_ShouldReturnSpanWithSizeHint()
    {
        // Arrange
        var writer = new SlidingBufferWriter();
        int sizeHint = 1024;
        // Act
        var span = writer.GetSpan(sizeHint);
        // Assert
        Assert.True(span.Length >= sizeHint);
    }
    [Fact]
    public void SlideChunk_ShouldResetWrittenBytesWhenBufferIsFull()
    {
        // Arrange
        var writer = new SlidingBufferWriter();
        byte[] data = new byte[writer.BytesLeft];
        // Act
        writer.Write(data, 0, data.Length);
        writer.SlideChunk();
        // Assert
        Assert.Equal(0, writer.WrittenBytes);
    }
    [Fact]
    public void Write_ShouldThrowExceptionWhenBufferOverflows()
    {
        // Arrange
        var writer = new SlidingBufferWriter();
        byte[] data = new byte[writer.BytesLeft + 1];
        // Act & Assert
        Assert.Throws<IndexOutOfRangeException>(() => writer.Write(data, 0, data.Length));
    }
}
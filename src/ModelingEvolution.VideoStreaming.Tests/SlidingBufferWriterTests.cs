using ModelingEvolution.VideoStreaming.VectorGraphics;

namespace ModelingEvolution.IO.Tests;

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
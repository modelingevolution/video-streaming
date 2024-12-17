using System.Runtime.InteropServices;
using FluentAssertions;
using ModelingEvolution.Drawing;
using ModelingEvolution.VideoStreaming.Hailo;

namespace ModelingEvolution.VideoStreaming.Tests;

public class StructMarshalTests
{
    [Fact]
    public void RectF()
    {
        Rectangle<float> r = new Rectangle<float>(0, 0, 0, 0);
        var t = Marshal.SizeOf(r);
        t.Should().Be(16);
    }
    [Fact]
    public void Stats()
    {
        int statsSize = Marshal.SizeOf<HailoProcessorStats>();
        int stageSize = Marshal.SizeOf<HailoProcessorStats.StageStats>();
        stageSize.Should().Be(44);
        statsSize.Should().Be(236);
    }

    [Fact]
    public void FrameIdentifier()
    {
        int size = Marshal.SizeOf<FrameIdentifier>();
        size.Should().Be(16);
    }
    
    [Theory]
    [InlineData(1, "100,0 ns")] // 1 tick = 100 nanoseconds
    [InlineData(10, "1,0 μs")]  // 10 ticks = 1 microsecond
    [InlineData(10000, "1,0 ms")] // 10000 ticks = 1 millisecond
    [InlineData(10000000, "1,0 s")] // 10000000 ticks = 1 second
    [InlineData(600000000, "1,0 min")] // 600000000 ticks = 1 minute
    [InlineData(36000000000, "1,0 h")] // 36000000000 ticks = 1 hour
    [InlineData(864000000000, "1,0 d")] // 864000000000 ticks = 1 day
    [InlineData(315360000000000, "1,0 y")] // 315360000000000 ticks = 1 year
    public void WithTimeSuffix_ShouldReturnCorrectFormat(long ticks, string expected)
    {
        // Arrange
        TimeSpan timeSpan = new TimeSpan(ticks);
        // Act
        string result = timeSpan.WithTimeSuffix(1);
        // Assert
        Assert.Equal(expected, result);
    }
};
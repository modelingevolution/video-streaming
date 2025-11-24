using FluentAssertions;

namespace ModelingEvolution.VideoStreaming.Tests;

public class FrameInfoTests
{
    [Fact]
    public void FullHd()
    {
        FrameInfo.FullHD.Width.Should().Be(1920);
        FrameInfo.FullHD.Height.Should().Be(1080);
        FrameInfo.FullHD.Stride.Should().Be(1920);
        FrameInfo.FullHD.Pixels.Should().Be(1920 * 1080);
        FrameInfo.FullHD.Yuv420.Should().Be(1920 * 1080 * 3/2);
        FrameInfo.FullHD.Rows.Should().Be(1080 + 1080 / 2);
    }
}
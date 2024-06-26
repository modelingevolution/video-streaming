using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Castle.Core.Logging;
using FluentAssertions;
using ModelingEvolution.IO.Tests;
using ModelingEvolution.VideoStreaming;
using NSubstitute.Core;

namespace ModelingEvolution.IO.Tests;

public class VideoAddressTests
{
    [Fact]
    public void ComplexUri()
    {
        string url = "shm+mjpeg://localhost/a?tags=elo_melo&resolution=SubHd";
        VideoAddress va = VideoAddress.Parse(url);
        va.Port.Should().Be(0);
        va.Codec.Should().Be(VideoCodec.Mjpeg);
        va.VideoTransport.Should().Be(VideoTransport.Shm);
        va.Host.Should().Be("localhost");
        va.StreamName.Should().Be("a");
        va.Resolution.Should().Be(VideoResolution.SubHd);
        va.Tags.Should().BeEquivalentTo(["elo_melo"]);

        va.ToString().Should().Be(url);
    }
}
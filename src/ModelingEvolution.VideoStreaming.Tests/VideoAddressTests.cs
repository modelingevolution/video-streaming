using FluentAssertions;

namespace ModelingEvolution.VideoStreaming.Tests;

public class VideoAddressTests
{
    [Fact]
    public void ComplexUri()
    {
        string url = "tcp+shm+mjpeg://localhost/a?tags=elo_melo&resolution=SubHd&file=e%3A%5C1.mp4&camera=1&video-api=OpenVidCam";
        VideoAddress va = VideoAddress.Parse(url);
        va.Port.Should().Be(0);
        va.Codec.Should().Be(VideoCodec.Mjpeg);
        va.VideoTransport.Should().Be(VideoTransport.Shm | VideoTransport.Tcp);
        va.Host.Should().Be("localhost");
        va.StreamName.Should().Be("a");
        va.Resolution.Should().Be(VideoResolution.SubHd);
        va.Tags.Should().BeEquivalentTo(["elo_melo"]);
        va.File.Should().Be("e:\\1.mp4");
        va.VideoSource.Should().Be(VideoSource.File);
        va.SourceApi.Should().Be(VideoSourceApi.OpenVidCam);
        va.ToString().Should().Be(url);
    }

    [Fact]
    public void ComplexUri2()
    {
        string url = "shm+mjpeg://localhost/a?tags=elo_melo&resolution=SubHd&file=e%3A%5C1.mp4&camera=1&video-api=OpenVidCam";
        VideoAddress va = VideoAddress.Parse(url);
        va.Port.Should().Be(0);
        va.Codec.Should().Be(VideoCodec.Mjpeg);
        va.VideoTransport.Should().Be(VideoTransport.Shm);
        va.Host.Should().Be("localhost");
        va.StreamName.Should().Be("a");
        va.Resolution.Should().Be(VideoResolution.SubHd);
        va.Tags.Should().BeEquivalentTo(["elo_melo"]);
        va.File.Should().Be("e:\\1.mp4");
        va.VideoSource.Should().Be(VideoSource.File);
        va.SourceApi.Should().Be(VideoSourceApi.OpenVidCam);
        va.ToString().Should().Be(url);
    }
    [Fact]
    public void ComplexUri3()
    {
        string url = "shm+mjpeg://localhost/default?video-api=Libcamera";
        VideoAddress va = VideoAddress.Parse(url);
        va.Port.Should().Be(0);
        va.Codec.Should().Be(VideoCodec.Mjpeg);
        va.VideoTransport.Should().Be(VideoTransport.Shm);
        va.Host.Should().Be("localhost");
        va.StreamName.Should().Be("default");
        va.Resolution.Should().Be(VideoResolution.FullHd);
        va.VideoSource.Should().Be(VideoSource.Camera);
        va.SourceApi.Should().Be(VideoSourceApi.Libcamera);
        va.ToString().Should().Be(url);
    }
}
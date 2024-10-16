using EventPi.Services.Camera;
using FluentAssertions;
using System;

namespace ModelingEvolution.VideoStreaming.Tests;

public class VideoRecordingDeviceTests
{
    [Fact]
    public void CanDeserialize()
    {
        string tmp = "localhost/cam-0";
        var result = VideoRecordingDevice.Parse(tmp, null);
        result.HostName.ToString().Should().Be("localhost");
        result.CameraNumber.Should().Be(0);
    }
}
public class VideoAddressTests
{
    [Fact]
    public void EqualsTest1()
    {
        string url = "shm+mjpeg://localhost/default";
        string url2 = "shm+mjpeg://localhost/default_1?camera=1";
        VideoAddress va = VideoAddress.Parse(url);
        VideoAddress va_roundTrip = VideoAddress.Parse(va.ToString());
        
        VideoAddress va2 = VideoAddress.Parse(url2);
        VideoAddress va2_roundTrip = VideoAddress.Parse(va2.ToString());
        
        va.Should().NotBe(va2);
        va_roundTrip.Should().Be(va);
        va2_roundTrip.Should().Be(va2);
    }

    
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
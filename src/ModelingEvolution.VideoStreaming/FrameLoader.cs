using System.Diagnostics;
using Emgu.CV;
using Emgu.CV.CvEnum;
using ModelingEvolution.VideoStreaming.Buffers;

namespace ModelingEvolution.VideoStreaming;

public static class FrameLoader
{
    
    public unsafe static MatYuvFrame Load(byte[] data)
    {
        // Load Mat from Memory<byte>, using EmguOpenCv
        using var mat = new Mat();
        CvInvoke.Imdecode(data, ImreadModes.Color, mat);

        var dst = new Mat();

        CvInvoke.CvtColor(mat, dst, ColorConversion.Bgr2YuvI420);
        Debug.WriteLine($"Stride: {dst.Step}, width: {dst.Width}");
        var s = (ulong)(mat.Width * mat.Height);
        s = s + s / 2;


        var metadata = new FrameMetadata(0, s, 0);
        var info = new FrameInfo(mat.Width, mat.Height, mat.Width);
        byte* ptr = (byte*)dst.DataPointer;
        byte p = ptr[0];
        var frame = new YuvFrame(metadata, info, (byte*)dst.GetDataPointer());
        return new MatYuvFrame(dst, frame);
    }
    public unsafe static MatYuvFrame Load(string imgFile)
    {
        using var mat = new Mat(imgFile);
        var dst =  new Mat();

        CvInvoke.CvtColor(mat, dst, ColorConversion.Bgr2YuvI420);
        Debug.WriteLine($"Stride: {dst.Step}, width: {dst.Width}");
        var s = (ulong)(mat.Width * mat.Height);
        s = s + s / 2;


        var metadata = new FrameMetadata(0, s, 0);
        var info = new FrameInfo(mat.Width, mat.Height, mat.Width);
        byte* ptr = (byte*)dst.DataPointer;
        byte p = ptr[0];
        var frame = new YuvFrame(metadata, info, (byte*)dst.GetDataPointer());
        return new MatYuvFrame(dst, frame);
    }
}
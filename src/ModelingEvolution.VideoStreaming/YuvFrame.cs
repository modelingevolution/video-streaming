using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using Emgu.CV;
using ModelingEvolution.VideoStreaming.Buffers;
using System.Drawing;

namespace ModelingEvolution.VideoStreaming;
public readonly struct MatFrame
{
    public readonly Mat Data;
    public readonly FrameMetadata Metadata;
    public readonly FrameInfo Info;

    public MatFrame(Mat data, FrameMetadata metadata, FrameInfo info)
    {
        Data = data;
        Metadata = metadata;
        Info = info;
    }
}
public unsafe readonly struct YuvFrame
{
    public readonly byte* Data;
    public readonly FrameMetadata Metadata;
    public readonly FrameInfo Info;
    public YuvFrame(in FrameMetadata metadata, in FrameInfo info, byte* data)
    {
        Data = data;
        Metadata = metadata;
        Info = info;
    }
    public MatFrame ToMatFrame()
    {
        return new MatFrame(ConvertMat3(), Metadata, Info);
    }
    public unsafe Mat ConvertMat3()
    {
        int frameSize = Info.Width*Info.Height;
        int chromaSize = frameSize / 4;
        int width = Info.Width;
        int height = Info.Height;

        byte* yPlane = Data;
        byte* uPlane = Data + frameSize;
        byte* vPlane = Data + frameSize + chromaSize;

        // Create Mats for Y, U, and V planes without copying data
        Mat yMat = new Mat(height, width, DepthType.Cv8U, 1, new IntPtr(yPlane), width);
        Mat uMat = new Mat(height / 2, width / 2, DepthType.Cv8U, 1, new IntPtr(uPlane), width / 2);
        Mat vMat = new Mat(height / 2, width / 2, DepthType.Cv8U, 1, new IntPtr(vPlane), width / 2);

        // Resize U and V planes to the same size as Y plane
        using Mat uMatResized = new Mat();
        CvInvoke.Resize(uMat, uMatResized, new Size(width, height), 0, 0, Inter.Linear);

        using Mat vMatResized = new Mat();
        CvInvoke.Resize(vMat, vMatResized, new Size(width, height), 0, 0, Inter.Linear);

        // Merge Y, U, and V planes into one YUV Mat
        using Mat yuvMat = new Mat();
        CvInvoke.Merge(new VectorOfMat(yMat, uMatResized, vMatResized), yuvMat);

        // Convert YUV Mat to BGR Mat
        Mat bgrMat = new Mat();
        CvInvoke.CvtColor(yuvMat, bgrMat, ColorConversion.Yuv2Bgr);

        return bgrMat;
    }
}

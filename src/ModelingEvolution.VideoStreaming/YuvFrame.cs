using System.Buffers;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using Emgu.CV;
using ModelingEvolution.VideoStreaming.Buffers;
using System.Drawing;
using System.Runtime.CompilerServices;

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

public readonly record struct YuvPixel(byte Y, byte U, byte V)
{
    public static implicit operator Color(YuvPixel pixel)
    {
        var c = pixel.Y - 16;
        var d = pixel.U - 128;
        var e = pixel.V - 128;

        var r = (298 * c + 409 * e + 128) >> 8;
        var g = (298 * c - 100 * d - 208 * e + 128) >> 8;
        var b = (298 * c + 516 * d + 128) >> 8;

        r = Math.Clamp(r, 0, 255);
        g = Math.Clamp(g, 0, 255);
        b = Math.Clamp(b, 0, 255);

        return Color.FromArgb(r, g, b);
    }
}

public delegate void ProcessPixel(in YuvPixelIterator iterator);
public readonly record struct YuvPixelIterator(Point Location, YuvPixel Pixel);

public record struct ManagedYuvFrame : IDisposable
{
    public readonly YuvFrame Frame;
    private readonly IMemoryOwner<byte> _owner;
    private MemoryHandle _handle;
    private bool _disposed = false;
    public ManagedYuvFrame(YuvFrame frame, IMemoryOwner<byte> owner, MemoryHandle handle)
    {
        Frame = frame;
        _owner = owner;
        _handle = handle;
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        _handle.Dispose();
        _owner.Dispose();
        _disposed = true;
    }

   
    public bool Disposed => _disposed;
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

internal static class MemPoolExtensions
{
    public static IMemoryOwner<byte> RentYuv420(this MemoryPool<byte> memoryPool, Size size)
    {
        int s = size.Width * size.Height;
        return memoryPool.Rent(s + s / 2);
    }
}

public unsafe readonly struct YuvFrame
{
    public readonly byte* Data;
    public readonly FrameMetadata Metadata;
    public readonly FrameInfo Info;

    public ManagedYuvFrame Resize(in Rectangle srcArea, in Size targetSz)
    {
        if (targetSz.Width >= Info.Width || targetSz .Height>= Info.Height)
        {
            throw new ArgumentException("New dimensions must be smaller than the original dimensions.");
        }

        var targetBuffer = MemoryPool<byte>.Shared.RentYuv420(targetSz);
        var handle = targetBuffer.Memory.Pin();
        var buffer = (byte*)handle.Pointer;
        var outputFrame = new YuvFrame(Metadata, 
            new FrameInfo(targetSz.Width, targetSz.Height, targetSz.Width),
            buffer);
        
        ResizeYUV420(Data, Info.Width, Info.Height, in srcArea,in targetSz, buffer);
        return new ManagedYuvFrame(outputFrame, targetBuffer, handle);
    }
    private static void ResizeYUV420(byte* sourceYUV, 
        int sourceWidth, 
        int sourceHeight, 
        in Rectangle selectedArea, 
        in Size targetSize, 
        byte* targetYUV)
    {
        int srcX = selectedArea.X;
        int srcY = selectedArea.Y;
        int srcW = selectedArea.Width;
        int srcH = selectedArea.Height;

        int dstW = targetSize.Width;
        int dstH = targetSize.Height;

        // Pointers for Y, U, V planes
        byte* srcYPlane = sourceYUV;
        byte* srcUPlane = srcYPlane + (sourceWidth * sourceHeight);
        byte* srcVPlane = srcUPlane + ((sourceWidth / 2) * (sourceHeight / 2));

        byte* dstYPlane = targetYUV;
        byte* dstUPlane = dstYPlane + (dstW * dstH);
        byte* dstVPlane = dstUPlane + ((dstW / 2) * (dstH / 2));

        // Resize Y plane using bilinear interpolation
        for (int y = 0; y < dstH; y++)
        {
            float srcRow = srcY + (y / (float)dstH) * srcH;
            int srcRowInt = (int)srcRow;
            float yAlpha = srcRow - srcRowInt;

            for (int x = 0; x < dstW; x++)
            {
                float srcCol = srcX + (x / (float)dstW) * srcW;
                int srcColInt = (int)srcCol;
                float xAlpha = srcCol - srcColInt;

                // Get the four nearest neighbors
                byte topLeft = srcYPlane[srcRowInt * sourceWidth + srcColInt];
                byte topRight = srcYPlane[srcRowInt * sourceWidth + (srcColInt + 1)];
                byte bottomLeft = srcYPlane[(srcRowInt + 1) * sourceWidth + srcColInt];
                byte bottomRight = srcYPlane[(srcRowInt + 1) * sourceWidth + (srcColInt + 1)];

                // Bilinear interpolation
                float top = topLeft + xAlpha * (topRight - topLeft);
                float bottom = bottomLeft + xAlpha * (bottomRight - bottomLeft);
                float interpolated = top + yAlpha * (bottom - top);

                dstYPlane[y * dstW + x] = (byte)interpolated;
            }
        }

        // Resize U and V planes (4:2:0 subsampling, so half resolution)
        for (int y = 0; y < dstH / 2; y++)
        {
            float srcRow = srcY / 2 + (y / (float)(dstH / 2)) * (srcH / 2);
            int srcRowInt = (int)srcRow;
            float yAlpha = srcRow - srcRowInt;

            for (int x = 0; x < dstW / 2; x++)
            {
                float srcCol = srcX / 2 + (x / (float)(dstW / 2)) * (srcW / 2);
                int srcColInt = (int)srcCol;
                float xAlpha = srcCol - srcColInt;

                // U Plane
                byte topLeftU = srcUPlane[srcRowInt * (sourceWidth / 2) + srcColInt];
                byte topRightU = srcUPlane[srcRowInt * (sourceWidth / 2) + (srcColInt + 1)];
                byte bottomLeftU = srcUPlane[(srcRowInt + 1) * (sourceWidth / 2) + srcColInt];
                byte bottomRightU = srcUPlane[(srcRowInt + 1) * (sourceWidth / 2) + (srcColInt + 1)];

                float topU = topLeftU + xAlpha * (topRightU - topLeftU);
                float bottomU = bottomLeftU + xAlpha * (bottomRightU - bottomLeftU);
                float interpolatedU = topU + yAlpha * (bottomU - topU);

                dstUPlane[y * (dstW / 2) + x] = (byte)interpolatedU;

                // V Plane (same logic as U plane)
                byte topLeftV = srcVPlane[srcRowInt * (sourceWidth / 2) + srcColInt];
                byte topRightV = srcVPlane[srcRowInt * (sourceWidth / 2) + (srcColInt + 1)];
                byte bottomLeftV = srcVPlane[(srcRowInt + 1) * (sourceWidth / 2) + srcColInt];
                byte bottomRightV = srcVPlane[(srcRowInt + 1) * (sourceWidth / 2) + (srcColInt + 1)];

                float topV = topLeftV + xAlpha * (topRightV - topLeftV);
                float bottomV = bottomLeftV + xAlpha * (bottomRightV - bottomLeftV);
                float interpolatedV = topV + yAlpha * (bottomV - topV);

                dstVPlane[y * (dstW / 2) + x] = (byte)interpolatedV;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Y(int x, int y)
    {
        if (x < 0 || x >= Info.Width || y < 0 || y >= Info.Height)
        {
            // Handle invalid coordinates, e.g., return 0 or throw an exception
            return 0;
        }

        // Calculate offset into the Y plane
        int yOffset = y * Info.Width + x;
        return Data[yOffset];
    }

    private byte* YOffset => Data;

    private byte* UOffset
    {
        get
        {
            int width = Info.Width;
            int height = Info.Height;
           
            return Data + width * height;
        }
    }
    private byte* VOffset
    {
        get
        {
            int width = Info.Width;
            int height = Info.Height;
            return Data + width * height + width / 4;
        }
    }
    public YuvPixel GetPixel(int x, int y)
    {
        int width = Info.Width;
        int height = Info.Height;
        // Check for valid coordinates
        if (x < 0 || x >= width || y < 0 || y >= height)
            throw new ArgumentOutOfRangeException(nameof(x), "x or y is out of range");
        // Calculate offsets
        int yOffset = y * width + x;
        int uvWidth = width / 2;
        int uvHeight = height / 2;
        int uvOffset = (y / 2) * uvWidth + (x / 2);
        // Access Y, U, and V values
        byte yVal = Data[yOffset];
        byte uVal = Data[width * height + uvOffset];
        byte vVal = Data[width * height + uvOffset + uvWidth * uvHeight];
        return new YuvPixel(yVal, uVal, vVal);
    }

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

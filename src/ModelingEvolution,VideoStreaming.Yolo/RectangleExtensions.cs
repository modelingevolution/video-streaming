using System.Drawing;
using System.Runtime.CompilerServices;

namespace ModelingEvolution_VideoStreaming.Yolo;

public static class RectangleExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sigmoid(this float value) => 1 / (1 + MathF.Exp(-value));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte GetLuminance(this float confidence) => (byte)((confidence * 255 - 255) * -1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rectangle TransformBy(this RectangleF bbox, Size modelImgSz, Rectangle interestRegion)
    {
        var scaleX = (float)interestRegion.Width / modelImgSz.Width;
        var scaleY = (float)interestRegion.Height / modelImgSz.Height;

        var x = interestRegion.X + (int)(bbox.X * scaleX);
        var y = interestRegion.Y + (int)(bbox.Y * scaleY);
        var width = (int)(bbox.Width * scaleX);
        var height = (int)(bbox.Height * scaleY);

        return new Rectangle(x, y, width, height);
    }
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rectangle NormalizedTransformBy(this RectangleF rect, Rectangle area)
    {
        int w = area.Width;
        int h = area.Height;
        int x = area.X + (int)(w * rect.X);
        int y = area.Y + (int)(h * rect.Y);
        int tw = (int)(w * rect.Width);
        int th = (int)(h * area.Height);
            
        return new Rectangle(x, y, tw, th);
    }
    public static Rectangle NormalizedScaleBy(this RectangleF rect, Size size)
    {
        var x = (int)(rect.X * size.Width);
        var y = (int)(rect.Y * size.Height);
        var width = (int)(rect.Width * size.Width);
        var height = (int)(rect.Height * size.Height);
        return new Rectangle(x, y, width, height);
    }
}
using System.Drawing;
using System.Globalization;
using System.Security.Cryptography;
using ModelingEvolution.Drawing;
using ModelingEvolution.VideoStreaming.Nal;

namespace ModelingEvolution.VideoStreaming.Tests;

public static class StreamExtensions
{
    public static Guid ComputeMd5(this Stream s)
    {
        using var hashAlgo = MD5.Create();
        hashAlgo.Initialize();
        return new Guid(hashAlgo.ComputeHash(s));
    }

    public static string ToAnnotationString(this Polygon<float> p)
    {
        // return a list of float values, that are composed out of points. 
        // and make it so that the decimal separator is always the same.
        CultureInfo culture = CultureInfo.InvariantCulture;
        var points = p.Points.Select(pt => $"{pt.X.ToString(culture)} {pt.Y.ToString(culture)}");
        return string.Join(' ', points);
    }
    public static Polygon<float> ScaleBy(this Polygon<float> p, SizeF s)
    {
        var scaledPoints = p.Points.Select(point => new Point<float>(point.X * s.Width, point.Y * s.Height)).ToArray();
        return new Polygon<float>(scaledPoints);
    }
    public static int WriteVideoKeyFrame(this byte[] data, int offset = 0)
    {
        VIDEO_KEY_FRAME.CopyTo(data,offset);
        return VIDEO_KEY_FRAME.Length;
    }
    public static int WriteRandomData(this byte[] data, int offset)
    {
        Random r = new Random();
        var len = data.Length - offset;
        r.NextBytes(data.AsSpan(offset, len));
        return len;
    }
    public static int WriteRandomData(this byte[] data, int offset, int len)
    {
        Random r = new Random();
        r.NextBytes(data.AsSpan(offset, len));
        return len;
    }
    public static readonly byte[] VIDEO_KEY_FRAME = new byte[] { 0x00, 0x00, 0x00, 0x01, (byte)NALType.SPS };
    public static int WriteRandomVideoData(this byte[] data, int offset = 0, int iterations = 3, byte[] key=null, int every=1024)
    {
        key ??= VIDEO_KEY_FRAME;
        Random r = new Random();


        int bytesWritten = 0;
            
        int c = 0;
        for(int i = offset; c < iterations;c++)
        {
            key.CopyTo(data, i);
            r.NextBytes(data.AsSpan(i+key.Length, every));
            
            i += every;
            i += key.Length;
            bytesWritten += every;
            bytesWritten += key.Length;
        }
            
        return bytesWritten;
    }
    public static int WriteRandomData(this Stream s, int bytes, byte[] key = null, int every = 1024)
    {
        Random r = new Random();
        byte[] buffer = new byte[every];

        int bytesWritten = 0;
        int writenFrames = 0;
        while(bytesWritten < bytes)
        {
            s.Write(key);
            r.NextBytes(buffer);
            s.Write(buffer, 0, buffer.Length);

            bytesWritten += buffer.Length;
            bytesWritten += key.Length;
            writenFrames += 1;
        }
        //Debug.WriteLine($"Frames: {writenFrames}");
        return bytesWritten;
    }
}
using Emgu.CV.Util;

namespace ModelingEvolution.VideoStreaming.VectorGraphics;

public static class Extensions
{
    public static List<VectorU16> ToVectorList(this VectorOfPoint p)
    {
        var points = new List<VectorU16>(p.Size);

        for (int i = 0; i < p.Size; i++)
        {
            points.Add(new  VectorU16((ushort)p[i].X, (ushort)p[i].Y));
        }

        return points;
    }
}
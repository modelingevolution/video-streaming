using System.Runtime.CompilerServices;
using Emgu.CV.Util;
using ProtoBuf;

[assembly: InternalsVisibleTo("TestProject1")]
namespace ModelingEvolution.VideoStreaming.VectorGraphics;

public static class Extensions
{
    public static List<Vector> ToVectorList(this VectorOfPoint p)
    {
        var points = new List<Vector>(p.Size);

        for (int i = 0; i < p.Size; i++)
        {
            points.Add(new  Vector((ushort)p[i].X, (ushort)p[i].Y));
        }

        return points;
    }
}
[ProtoContract]
public readonly struct Vector
{
    [ProtoMember(1)]
    public ushort X { get; init; }
    [ProtoMember(2)]
    public ushort Y { get; init; }

    public Vector()
    {
        
    }
    public Vector(ushort x, ushort y)
    {
        X = x;
        Y = y;
    }
}
using System.Drawing;
using System.Numerics;
using System.Text;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using ModelingEvolution.Drawing;
using ProtoBuf;

namespace ModelingEvolution.VideoStreaming.VectorGraphics;


[ProtoContract]
public record struct Polygon : IRenderItem
{
    public Polygon()
    {
        
    }

    public Polygon<float> ToPolygonF()
    {
        var floatPoints = Points.Select(p => (ModelingEvolution.Drawing.Point<float>)p)
            .ToArray();
        return new Polygon<float>(floatPoints);
    }
    public Polygon(List<VectorU16> points)
    {
        this.Points = points;
    }
    public Polygon ScaleBy(SizeF size)
    {
        for (int i = 0; i < this.Points.Count; i++) 
            this.Points[i] *= size;

        return this;
    }
    public Polygon ScaleBy(Size size)
    {
        for (int i = 0; i < this.Points.Count; i++) 
            this.Points[i] *= size;

        return this;
    }

    
    public Polygon Offset(VectorU16 offset)
    {
        for (var i = 0; i < Points.Count; i++)
            Points[i] += offset;

        return this;
    }

    [ProtoMember(1)] 
    public List<VectorU16> Points { get; init; } = new List<VectorU16>();

    public static Polygon GenerateRandom(int count)
    {
        List<VectorU16> tmp = new List<VectorU16>(count);
        for (int i = 0; i < count; i++)
            tmp.Add(new VectorU16()
            {
                X = (ushort)Random.Shared.Next(0, ushort.MaxValue),
                Y = (ushort)Random.Shared.Next(0, ushort.MaxValue)
            });
        return new Polygon() { Points = tmp };
    }

    public void Render(ICanvas canvas, DrawContext? context)
    {
        canvas.DrawPolygon(Points,context.Stroke);
    }

    public ushort Id => 2;

    public override string ToString()
    {
        return string.Join(' ', this.Points.Select(x => $"{x.X} {x.Y}"));
    }

    public string ToSvg()
    {
        // Generate Svg type path string.
        StringBuilder sb = new StringBuilder("M");
        foreach (var point in Points)
        {
            sb.Append($" {point.X} {point.Y}");
        }

        sb.Append(" Z");
        return sb.ToString();
    }
}
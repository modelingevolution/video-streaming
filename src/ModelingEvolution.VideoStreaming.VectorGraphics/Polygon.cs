using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using ModelingEvolution.Drawing;
using ModelingEvolution.VideoStreaming.Buffers;
using ProtoBuf;

namespace ModelingEvolution.VideoStreaming.VectorGraphics;

public record SegmentationPolygon : IDisposable
{
    
    public Polygon Polygon { get; init; }
    public Size ImageSize { get; set; }
    public Resolution Resolution { get; init; }

    public void TransformBy(in System.Drawing.Rectangle interestRegion)
    {
        this.Resize(interestRegion.Size);
        this.Polygon.Offset(interestRegion.Location);
        Debug.Assert(Polygon.Points.Count > 2);
    }
    public void Resize(Size newSize)
    {
        var ratio = new SizeF(((float)newSize.Width) / ImageSize.Width, ((float)newSize.Height) / ImageSize.Height);
        Polygon.ScaleBy(ratio);
        ImageSize = newSize;
    }

    public Polygon<float> NormalizedPolygon()
    {
        var polygonF = Polygon.ToPolygonF();
        var ratio = new Size<float>(1f / ImageSize.Width, 1f / ImageSize.Height);
        return polygonF * ratio;
    }
    
    public SegmentationPolygon(ManagedArray<VectorU16> points, in Size size)
    {
        Debug.Assert(points.Count > 2);
        Polygon = new Polygon(points);
        ImageSize = size;
        Resolution = new Resolution(size.Width, size.Height);
        
        Debug.Assert(Polygon.Points.Count > 2);
    }

    private void Dispose(bool disposing)
    {
        if (disposing) Polygon.Dispose();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

[ProtoContract]
public record class Polygon : IRenderItem, IDisposable
{
    public Polygon()
    {
        
    }

    public static Polygon From(IEnumerable<VectorU16> points)
    {
        if (points is VectorU16[] u16s)
            return new Polygon(u16s);
        else if (points is ManagedArray<VectorU16> ma)
            return new Polygon(ma);
        return new Polygon(points.ToArray());
    }

    
    public Polygon<float> ToPolygonF()
    {
        var floatPoints = this.Points.Select(p => (ModelingEvolution.Drawing.Point<float>)p)
            .ToArray();
        return new Polygon<float>(floatPoints);
    }
    public Polygon(VectorU16[] points)
    {
        this.Points = new ManagedArray<VectorU16>(points);
        
    }
    public Polygon(ManagedArray<VectorU16> points)
    {
        this.Points = points;
    }
    public void ScaleBy(SizeF size)
    {
        for (int i = 0; i < Points.Count; i++) 
            this.Points[i] *= size;

    }
    public void ScaleBy(Size size)
    {
        for (int i = 0; i < Points.Count; i++) 
            this.Points[i] *= size;

    }

    public VectorU16 this[int index] => Points[index];
    
    public void Offset(VectorU16 offset)
    {
        for (var i = 0; i < Points.Count; i++)
            Points[i] += offset;
    }

    public string ToAnnotationString()
    {
        CultureInfo culture = CultureInfo.InvariantCulture;
        var points = this.Points.Select(pt => $"{pt.X.ToString(culture)} {pt.Y.ToString(culture)}");
        return string.Join(' ', points);
    }

    [ProtoMember(1)] 
    public ManagedArray<VectorU16> Points { get; init; }

    

    public static Polygon GenerateRandom(int count)
    {
        var tmp = new ManagedArray<VectorU16>(count);
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

    public void Dispose()
    {
        Points.Dispose();
    }


    public string ToSvg()
    {
        // Generate Svg type path string.
        StringBuilder sb = new StringBuilder("M");
        foreach (var point in this.Points) 
            sb.Append($" {point.X} {point.Y}");

        sb.Append(" Z");
        return sb.ToString();
    }
}
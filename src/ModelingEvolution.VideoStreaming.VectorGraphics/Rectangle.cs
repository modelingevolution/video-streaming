using System.Drawing;
using ModelingEvolution.Drawing;
using ProtoBuf;

namespace ModelingEvolution.VideoStreaming.VectorGraphics;

[ProtoContract]
public readonly struct Rectangle
{
    public static implicit operator System.Drawing.Rectangle(Rectangle r)
    {
        return new System.Drawing.Rectangle(r.X, r.Y, r.Width, r.Height);
    }

    public Rectangle()
    {
        
    }

    public Rectangle(ushort x, ushort y, ushort width, ushort height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
    public static Rectangle operator +(in Rectangle a,in Size<float> b)
    {
        return new Rectangle(a.X, a.Y, (ushort)(a.Width + b.Width), (ushort)(a.Height + b.Height));
    }
    public static Rectangle operator -(in Rectangle a,in Size<float> b)
    {
        return new Rectangle(
            a.X,
            a.Y,
            (ushort)Math.Max(0, a.Width - b.Width),
            (ushort)Math.Max(0, a.Height - b.Height)
        );
    }
    public static Rectangle operator +(in Rectangle a, in Size b)
    {
        return new Rectangle(a.X, a.Y, (ushort)(a.Width + b.Width), (ushort)(a.Height + b.Height));
    }
    public static Rectangle operator -(in Rectangle a, in Size b)
    {
        return new Rectangle(
            a.X,
            a.Y,
            (ushort)Math.Max(0, a.Width - b.Width),
            (ushort)Math.Max(0, a.Height - b.Height)
        );
    }
    [ProtoMember(1)]
    public ushort X { get; init; }
    
    [ProtoMember(2)]
    public ushort Y { get; init; }

    [ProtoMember(3)]
    public ushort Width { get; init; }
    
    [ProtoMember(4)]
    public ushort Height { get; init; }

    public override string ToString()
    {
        return $"[{X} {Y} {Width} {Height}]";
    }
}
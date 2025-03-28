﻿using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using ProtoBuf;

[assembly: InternalsVisibleTo("TestProject1")]
namespace ModelingEvolution.VideoStreaming.VectorGraphics;

[ProtoContract]
public readonly record struct VectorU16
{
    [ProtoMember(1)]
    public ushort X { get; init; }
    [ProtoMember(2)]
    public ushort Y { get; init; }

    public static explicit operator ModelingEvolution.Drawing.Point<float>(VectorU16 vector)
    {
        var x = (float)vector.X;
        var y = (float)vector.Y;
        return new Drawing.Point<float>(x, y);
    }
    public static explicit operator VectorU16(ModelingEvolution.Drawing.Point<float> vector)
    {
        ushort x = (ushort)vector.X;
        ushort y = (ushort)vector.Y;
        return new VectorU16(x, y);
    }
    public static explicit operator ModelingEvolution.Drawing.Vector<float>(VectorU16 vector)
    {
        var x = (float)vector.X;
        var y = (float)vector.Y;
        return new Drawing.Vector<float>(x,y);
    }
    public static implicit operator VectorU16(System.Drawing.Point point)
    {
        return new VectorU16((ushort)point.X, (ushort)point.Y);
    }
    public static VectorU16 operator +(VectorU16 a, VectorU16 b)
    {
        return new VectorU16((ushort)(a.X + b.X), (ushort)(a.Y + b.Y));
    }
    public static VectorU16 operator -(VectorU16 a, VectorU16 b)
    {
        return new VectorU16((ushort)(a.X - b.X), (ushort)(a.Y - b.Y));
    }

    public static VectorU16 operator *(VectorU16 a, Size size)
    {
        return new VectorU16((ushort)(a.X * size.Width), (ushort)(a.Y * size.Height));
    }
    public static VectorU16 operator *(VectorU16 a, SizeF size)
    {
        return new VectorU16((ushort)(a.X * size.Width), (ushort)(a.Y * size.Height));
    }

    public static VectorU16 operator *(VectorU16 a, ushort scalar)
    {
        return new VectorU16((ushort)(a.X * scalar), (ushort)(a.Y * scalar));
    }
    public static ushort Dot(VectorU16 a, VectorU16 b)
    {
        return (ushort)(a.X * b.X + a.Y * b.Y);
    }

    public VectorU16()
    {
        
    }
    public VectorU16(ushort x, ushort y)
    {
        X = x;
        Y = y;
    }
}
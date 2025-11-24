using Emgu.CV.Util;
using Microsoft.Extensions.DependencyInjection;
using ModelingEvolution.VideoStreaming.Buffers;
using ModelingEvolution.VideoStreaming.VectorGraphics;

namespace ModelingEvolution.VideoStreaming.Yolo;

public static class ContainerExtensions
{
    public static IServiceCollection AddYolo(this IServiceCollection services)
    {
        services.AddSingleton<ModelFactory>();
        return services;
    }
}
public static class Ext
{
    public static VectorU16[] ToVectorArray(this VectorOfPoint p)
    {
        var points = new VectorU16[p.Size];

        for (int i = 0; i < p.Size; i++)
        {
            points[i] = new VectorU16((ushort)p[i].X, (ushort)p[i].Y);
        }

        return points;
    }
    public static ManagedArray<VectorU16> ToArrayBuffer(this VectorOfPoint p)
    {
        var points = new ManagedArray<VectorU16>(p.Size);

        for (int i = 0; i < p.Size; i++) 
            points.Add(p[i]);

        return points;
    }
}
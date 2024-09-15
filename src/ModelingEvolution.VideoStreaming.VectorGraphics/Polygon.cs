using ProtoBuf;

namespace ModelingEvolution.VideoStreaming.VectorGraphics;

[ProtoContract]
public class Polygon : IRenderItem
{
    [ProtoMember(1)] 
    public List<Vector> Points { get; init; } = new List<Vector>();

    public static Polygon GenerateRandom(int count)
    {
        List<Vector> tmp = new List<Vector>(count);
        for (int i = 0; i < count; i++)
            tmp.Add(new Vector()
            {
                X = (ushort)Random.Shared.Next(0, ushort.MaxValue),
                Y = (ushort)Random.Shared.Next(0, ushort.MaxValue)
            });
        return new Polygon() { Points = tmp };
    }

    public void Render(ICanvas canvas, DrawContext? context)
    {
        
    }
}
using ProtoBuf;

namespace ModelingEvolution.VideoStreaming.VectorGraphics;

[ProtoContract]
public class Draw<TObject> : IRenderOp where TObject: IRenderItem
{
    [ProtoMember(1)]
    public TObject Value { get; init; }

    [ProtoMember(2)]
    public DrawContext? Context { get; init; }

    public void Render(ICanvas canvas)
    {
        Value.Render(canvas,Context);
    }

    public ushort Id => Value.Id;
}
using ProtoBuf;

namespace ModelingEvolution.VideoStreaming.VectorGraphics;

[ProtoContract]
public class Draw<TObject> : IRenderOp, IDisposable
    where TObject: IRenderItem
{
    [ProtoMember(1)]
    public TObject Value { get; init; }

    [ProtoMember(2)]
    public DrawContext? Context { get; init; }

    //[ProtoMember(3)]
    //public byte ContextId { get; set; }
    
    public void Render(ICanvas canvas)
    {
        Value.Render(canvas,Context);
    }

    public ushort Id => Value.Id;
    public void Dispose()
    {
        if(Value is IDisposable d) d.Dispose();
    }
}
using ProtoBuf;

namespace ModelingEvolution.VideoStreaming.VectorGraphics;

[ProtoContract]
public readonly struct Rectangle
{
    [ProtoMember(1)]
    public ushort X { get; init; }
    
    [ProtoMember(2)]
    public ushort Y { get; init; }

    [ProtoMember(3)]
    public ushort Width { get; init; }
    
    [ProtoMember(4)]
    public ushort Height { get; init; }

}
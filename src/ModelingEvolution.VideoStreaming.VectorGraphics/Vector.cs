using System.Runtime.CompilerServices;
using ProtoBuf;

[assembly: InternalsVisibleTo("TestProject1")]
namespace ModelingEvolution.VideoStreaming.VectorGraphics;

[ProtoContract]
public readonly struct Vector
{
    [ProtoMember(1)]
    public ushort X { get; init; }
    [ProtoMember(2)]
    public ushort Y { get; init; }

}
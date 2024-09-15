﻿namespace ModelingEvolution.VideoStreaming.VectorGraphics;

public interface ISerializer
{
    public object Deserialize(ref ReadOnlySpan<byte> data, ushort type);
}
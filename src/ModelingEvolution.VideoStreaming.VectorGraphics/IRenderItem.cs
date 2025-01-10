﻿namespace ModelingEvolution.VideoStreaming.VectorGraphics;

public interface IRenderItem
{
    void Render(ICanvas canvasChannel, DrawContext? context);
    ushort Id { get; }
}
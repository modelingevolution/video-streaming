namespace ModelingEvolution.VideoStreaming.VectorGraphics;

public interface IRenderItem
{
    void Render(ICanvas canvas, DrawContext? context);
}
namespace ModelingEvolution.VideoStreaming.VectorGraphics;

public interface IRenderOp
{
    void Render(ICanvas canvas);
    ushort Id { get; }
    
}
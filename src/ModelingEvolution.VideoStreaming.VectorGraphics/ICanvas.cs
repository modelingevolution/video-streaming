namespace ModelingEvolution.VideoStreaming.VectorGraphics;

public interface ICanvas
{
    void DrawPolygon(IEnumerable<Vector> points, RgbColor? color = null);
    void End();
    void Begin(ulong frameNr);
    void DrawText(string text, ushort x = 0, ushort y = 0, ushort size = 12, RgbColor? color = null);
}
namespace ModelingEvolution.VideoStreaming.VectorGraphics;

public interface ICanvas
{
    void DrawPolygon(IEnumerable<VectorU16> points, RgbColor? color = null, byte? layerId = null);
    void End(byte? layerId = null);
    void Begin(ulong frameNr, byte? layerId = null);
    void DrawRectangle(System.Drawing.Rectangle rect, RgbColor? color = null, byte? layerId = null);
    void DrawText(string text, ushort x = 0, ushort y = 0, ushort size = 12, RgbColor? color = null, byte? layerId = null);
}
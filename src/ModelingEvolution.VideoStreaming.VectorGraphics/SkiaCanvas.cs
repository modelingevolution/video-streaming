using SkiaSharp;

namespace ModelingEvolution.VideoStreaming.VectorGraphics;

public class SkiaCanvas : ICanvas
{
    private List<IRenderOp> _opRenderBuffer = new();
    private List<IRenderOp> _opSinkBuffer = new();
    private SKCanvas _canvas;
    public void Add(IRenderOp op) => _opSinkBuffer.Add(op);

    /// <summary>
    /// this method swap buffers. New buffer is cleared before swap.
    /// </summary>
    /// <returns></returns>
    public void Complete()
    {
        var temp = _opRenderBuffer;
        _opRenderBuffer = _opSinkBuffer;
        temp.Clear();
        _opSinkBuffer = temp;
    }

    public void Render(SKCanvas canvas)
    {
        _canvas = canvas;
        Render();
    }
    public void Render()
    {
        var ops = _opRenderBuffer;
        foreach (var i in ops)
        {
            i.Render(this);
        }
    }

 

    public void DrawText(string text, ushort x, ushort y, ushort size, RgbColor? color)
    {
        using var paint = new SKPaint
        {
            TextSize = size,
            Color = color ?? RgbColor.Black
        };
        using var font = new SKFont(SKTypeface.Default, size);
        _canvas.DrawText(text, x,y,font, paint);
    }

    public void DrawPolygon(IEnumerable<Vector> points)
    {
        
    }
}
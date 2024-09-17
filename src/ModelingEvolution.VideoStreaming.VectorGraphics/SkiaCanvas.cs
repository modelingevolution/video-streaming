using SkiaSharp;
using System.Drawing;
using static ModelingEvolution.VideoStreaming.VectorGraphics.ProtoStreamClient;
using static System.Net.Mime.MediaTypeNames;

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
        Console.WriteLine("Complete");
    }

    public void Render(SKCanvas canvas)
    {
        _canvas = canvas;
        End();
    }
    public void End()
    {
        var ops = _opRenderBuffer;
        foreach (var i in ops)
        {
            i.Render(this);
        }
    }

    public void Begin(ulong frameNr)
    {
        Console.WriteLine($"Begin: {frameNr}");
    }


    public void DrawText(string text, ushort x, ushort y, ushort size, RgbColor? color)
    {
        Console.WriteLine($"Text: {text}");
        using var paint = new SKPaint
        {
            TextSize = size,
            Color = color ?? RgbColor.Black
        };
        using var font = new SKFont(SKTypeface.Default, size);
        _canvas.DrawText(text, x,y,font, paint);
    }

    public void DrawPolygon(IEnumerable<Vector> points, RgbColor? color = null)
    {
        
        using var paint = new SKPaint
        {
            Color = color ?? RgbColor.Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        using SKPath p = new SKPath();
        bool isFirstPoint = true;
        // implement
        foreach (var point in points)
        {
            if (isFirstPoint)
            {
                p.MoveTo(point.X, point.Y);
                isFirstPoint = false;
            }
            else
            {
                p.LineTo(point.X, point.Y);
            }
        }
        p.Close();
        //Console.WriteLine($"Points: {p.Points.Length}");
        _canvas.DrawPath(p,paint);
    }
}
using System.Collections.Concurrent;
using SkiaSharp;
using System.Drawing;
using System.Runtime.CompilerServices;
using static ModelingEvolution.VideoStreaming.VectorGraphics.ProtoStreamClient;
using static System.Net.Mime.MediaTypeNames;

namespace ModelingEvolution.VideoStreaming.VectorGraphics;

public class SkiaCanvas : ICanvas
{
    class Layer
    {
        public List<IRenderOp> RenderBuffer = new();
        public List<IRenderOp> OpSink = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Swap()
        {
            var temp = RenderBuffer;
            RenderBuffer = OpSink;
            foreach(var d in  temp.OfType<IDisposable>()) d.Dispose();
            
            temp.Clear();
            OpSink = temp;
        }
    }

    private readonly Layer[] _layers = new Layer[255];
    private volatile byte[] _layerIx = Array.Empty<byte>();
    private readonly object _sync = new();
    private byte[] LayerIx => _layerIx;
    

    private Layer GetLayer(byte ix)
    {
        if (_layers[ix] != null!)
            return _layers[ix];
        lock (_sync)
        {
            if (_layers[ix] != null!)
                return _layers[ix];
            else
            {
                var l = new Layer();
                _layers[ix] = l;
                var n = new byte[_layerIx.Length + 1];
                Array.Copy(_layerIx, n,_layerIx.Length);
                n[_layerIx.Length] = ix;
                Array.Sort(n);
                _layerIx = n;
                return l;
            }
        }
        
    }


    private readonly PeriodicConsoleWriter _writer = new(TimeSpan.FromSeconds(30));
    private SKCanvas _canvas;
    private byte DefaultLayerId { get; set; }
    public void Add(IRenderOp op, byte? layerId) => GetLayer(layerId ?? DefaultLayerId).OpSink.Add(op);



    public void Render(SKCanvas canvas)
    {
        _canvas = canvas;
        var ls = LayerIx;
        for (byte il = 0; il < ls.Length; il++)
        {
            var layer = GetLayer(il);
            var ops = layer.RenderBuffer;
            for (var index = 0; index < ops.Count; index++)
            {
                var i = ops[index];
                i.Render(this);
            }
        }
    }
    public void End(byte? layerId)
    {
        GetLayer(layerId ?? DefaultLayerId).Swap();
    }

    public void Begin(ulong frameNr, byte? layerId)
    {
        _writer.WriteLine($"Render frame: {frameNr}");
    }

    public void DrawRectangle(System.Drawing.Rectangle rect, RgbColor? color, byte? layerId)
    {
        throw new NotImplementedException();
    }


    public void DrawText(string text, ushort x, ushort y, ushort size, RgbColor? color, byte? layerId=null)
    {
        //Console.WriteLine($"Text: {text}");
        using var paint = new SKPaint
        {
            TextSize = size,
            Color = color ?? RgbColor.Black
        };
        using var font = new SKFont(SKTypeface.Default, size);
        _canvas.DrawText(text, x,y,font, paint);
    }

    public void DrawPolygon(IEnumerable<VectorU16> points, RgbColor? color = null, byte? layerId=null)
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
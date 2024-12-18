using System.Runtime.CompilerServices;
using ModelingEvolution.VideoStreaming.Buffers;

namespace ModelingEvolution.VideoStreaming.VectorGraphics;

// When drawing on a layer we need to start with begin and finish with appropriate end. 
// the implementation is not thread safe. 
public interface ICanvas
{
    // Default layer
    byte LayerId { get; set; }
    object Sync { get; }
    // draws a polygon on a layer
    void DrawPolygon(IEnumerable<VectorU16> points, RgbColor? color = null, ushort width = 1, byte? layerId = null);
    
    // marks the ends of drawing on a layer
    void End(byte? layerId = null);
    
    // begins drawing on a layer
    void Begin(ulong frameNr, byte? layerId = null);
   

    // draws rectangle on a layer
    void DrawRectangle(System.Drawing.Rectangle rect, RgbColor? color = null, byte? layerId = null);
    
    // draws text on a layer
    void DrawText(string text, ushort x = 0, ushort y = 0, ushort size = 12, RgbColor? color = null, byte? layerId = null);
}
public static class CanvasExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DrawingBatchScope BeginScope(this ICanvas canvas, ulong frameNr, byte? layerId) =>
        new DrawingBatchScope(canvas, layerId ?? canvas.LayerId, frameNr);
}
// no closure, no boxing, no memory allocation.
public struct DrawingBatchScope : IDisposable
{
    private readonly ICanvas _canvas;
    private ManagedArrayStruct<DrawCommand> _queue;

    public DrawingBatchScope(ICanvas canvas, byte layer, ulong frame)
    {
        LayerId = layer;
        FrameId = frame;
        _canvas = canvas;
        _queue = new ManagedArrayStruct<DrawCommand>(8);
    }

    public byte LayerId { get; }
    public ulong FrameId { get; }

    public void DrawPolygon(IEnumerable<VectorU16> points, RgbColor? color = null)
    {
        _queue.Add(new DrawCommand(DrawType.Polygon, points, null, null, null, color, LayerId));
    }

    public void DrawRectangle(System.Drawing.Rectangle rect, RgbColor? color = null)
    {
        _queue.Add(new DrawCommand(DrawType.Rectangle, null, rect, null, null, color, LayerId));
    }

    public void DrawText(string text, ushort x = 0, ushort y = 0, ushort size = 12, RgbColor? color = null)
    {
        _queue.Add(new DrawCommand(DrawType.Text, null, null, text, (x, y, size), color, LayerId));
    }

    public void Dispose()
    {
        lock (_canvas.Sync)
        {
            _canvas.Begin(FrameId, LayerId);
            for (int i = 0; i < _queue.Count; i++)
            {
                var command = _queue[i];
                command.Execute(_canvas);
            }

            _canvas.End(LayerId);
            _queue.Dispose();
        }
    }

    // Command struct to store all required data for drawing
    public struct DrawCommand
    {
        private readonly DrawType _type;
        private readonly IEnumerable<VectorU16>? _points;
        private readonly System.Drawing.Rectangle? _rect;
        private readonly string? _text;
        private readonly (ushort X, ushort Y, ushort Size)? _textParams;
        private readonly RgbColor? _color;
        private readonly byte _layer;

        public DrawCommand(
            DrawType type,
            IEnumerable<VectorU16>? points,
            System.Drawing.Rectangle? rect,
            string? text,
            (ushort X, ushort Y, ushort Size)? textParams,
            RgbColor? color,
            byte layer)
        {
            _type = type;
            _points = points;
            _rect = rect;
            _text = text;
            _textParams = textParams;
            _color = color;
            _layer = layer;
        }

        public void Execute(ICanvas canvas)
        {
            switch (_type)
            {
                case DrawType.Polygon:
                    canvas.DrawPolygon(_points!, _color, _layer);
                    break;
                case DrawType.Rectangle:
                    canvas.DrawRectangle(_rect!.Value, _color, _layer);
                    break;
                case DrawType.Text:
                    var (x, y, size) = _textParams!.Value;
                    canvas.DrawText(_text!, x, y, size, _color, _layer);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported draw type.");
            }
        }
    }
    public enum DrawType
    {
        Polygon,
        Rectangle,
        Text
    }
}

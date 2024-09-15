namespace ModelingEvolution.VideoStreaming.VectorGraphics;

public class RemoteCanvas(Action<IRenderOp> pushObject, Action onComplete) : ICanvas
{
    
    public void DrawPolygon(IEnumerable<Vector> points)
    {
       
    }

    public void Render() => onComplete();
    public void DrawText(string text, ushort x = 0, ushort y = 0, ushort size = 12, RgbColor? color = null)
    {
        var renderOp = new Draw<Text>
        {
            Value = new Text { Content = text },
            Context = new DrawContext
            {
                FontSize = size, 
                FontColor = color ?? RgbColor.Black, 
                Offset = new Vector { X = x, Y = y }
            }
        };
        pushObject(renderOp);
    }

    
}
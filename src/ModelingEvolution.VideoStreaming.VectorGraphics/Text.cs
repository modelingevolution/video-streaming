using ProtoBuf;

namespace ModelingEvolution.VideoStreaming.VectorGraphics;

[ProtoContract]
public class Text : IRenderItem
{
    [ProtoMember(1)]
    public string Content { get; init; }

    public void Render(ICanvas canvas, DrawContext? context)
    {
        var offsetX = context?.Offset?.X ?? 0;
        var offsetY = context?.Offset?.Y ?? 0;
        var fontSize = context?.FontSize ?? 12;
        var fontColor = context?.FontColor ?? RgbColor.Black;

        canvas.DrawText(Content, offsetX, offsetY, fontSize, fontColor);
    }

    public ushort Id => 1;
}
using ProtoBuf;

namespace ModelingEvolution.VideoStreaming.VectorGraphics;

[ProtoContract]
public class Text : IRenderItem
{
    [ProtoMember(1)]
    public string Content { get; init; }

    public void Render(ICanvas canvasChannel, DrawContext? context)
    {
        var offsetX = context?.Offset?.X ?? 0;
        var offsetY = context?.Offset?.Y ?? 0;
        var fontSize = context?.FontSize ?? 12;
        var fontColor = context?.FontColor ?? RgbColor.Black;

        Console.WriteLine($"Drawing text: {Content} with context: {context}");
        canvasChannel.DrawText(Content, offsetX, offsetY, fontSize, fontColor);
    }

    public ushort Id => 1;
}
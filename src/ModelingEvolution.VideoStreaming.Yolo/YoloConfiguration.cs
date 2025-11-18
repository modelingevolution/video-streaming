using System.Drawing;

namespace ModelingEvolution.VideoStreaming.Yolo;

public class YoloOnnxConfiguration
{
    /// <summary>
    /// Specify the minimum confidence value for including a result. Default is 0.3f.
    /// </summary>
    public float Confidence { get; set; } = .3f;

    /// <summary>
    /// Specify the minimum IoU value for Non-Maximum Suppression (NMS). Default is 0.45f.
    /// </summary>
    public float IoU { get; set; } = .45f;


    public Size ImageSize { get; init; } = new Size(640, 640);

}
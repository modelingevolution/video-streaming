using System.Drawing;

namespace ModelingEvolution_VideoStreaming.Yolo;

public class Detection : YoloPrediction, IYoloPrediction<Detection>
{
    public required Rectangle Bounds { get; init; }

    static string IYoloPrediction<Detection>.Describe(Detection[] predictions) => predictions.Summary();

    protected virtual void Dispose(bool disposing) { }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace ModelingEvolution_VideoStreaming.Yolo;

public static class Extensions
{
    public static string GetOnnxModel(this IConfiguration config)
    {
        return config.GetValue<string>("OnnxPath");
    }

    public static float GetAiConfidenceThreshold(this IConfiguration config)
    {
        var str = config.GetValue<string>("ConfidenceThreshold") ?? "0.9";
        return float.Parse(str, CultureInfo.InvariantCulture);
    }
}
public record ModelPerformance
{
    private TimeSpan _preProcessingTime;
    private TimeSpan _interferenceTime;
    private TimeSpan _postProcessingTime;

    public ref struct MeasureScope : IDisposable
    {
        private readonly Action<TimeSpan> _onFinished;
        private readonly DateTime _start;
            
        public MeasureScope(Action<TimeSpan> onFinished)
        {
            _onFinished = onFinished;
            _start = DateTime.Now;
        }

        public void Dispose()
        {
            _onFinished(DateTime.Now.Subtract(_start));
        }
    }

    public TimeSpan PreProcessingTime => _preProcessingTime;

    public TimeSpan InterferenceTime => _interferenceTime;

    public TimeSpan PostProcessingTime => _postProcessingTime;
    public TimeSpan Total => _interferenceTime + _preProcessingTime + _postProcessingTime;
    public MeasureScope MeasurePreProcessing() => new MeasureScope(x => _preProcessingTime=x);
    public MeasureScope MeasureInterference() => new MeasureScope(x => _interferenceTime = x);
    public MeasureScope MeasurePostProcessing() => new MeasureScope(x => _postProcessingTime = x);

}
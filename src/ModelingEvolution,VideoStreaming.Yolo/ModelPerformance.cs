namespace ModelingEvolution_VideoStreaming.Yolo;

public interface IModelPerformance
{
    TimeSpan PreProcessingTime { get; }
    TimeSpan InterferenceTime { get; }
    TimeSpan PostProcessingTime { get; }
    TimeSpan SignalProcessingTime { get; }
    TimeSpan Total { get; }
}

public record ModelPerformance : IModelPerformance
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

    public override string ToString()
    {
        int pre = (int)_preProcessingTime.TotalMilliseconds;
        int ex = (int)_interferenceTime.TotalMilliseconds;
        int post = (int)_postProcessingTime.TotalMilliseconds;
        int t = (int)Total.TotalMilliseconds;
        return $"Performance: {pre} / {ex} / {post}  {t} ms";
    }

    public TimeSpan PreProcessingTime => _preProcessingTime;
    public TimeSpan SignalProcessingTime { get; set; }
    public TimeSpan InterferenceTime => _interferenceTime;

    public TimeSpan PostProcessingTime => _postProcessingTime;
    public TimeSpan Total => _interferenceTime + _preProcessingTime + _postProcessingTime;
    public MeasureScope MeasurePreProcessing() => new MeasureScope(x => _preProcessingTime=x);
    public MeasureScope MeasureInterference() => new MeasureScope(x => _interferenceTime = x);
    public MeasureScope MeasurePostProcessing() => new MeasureScope(x => _postProcessingTime = x);

}
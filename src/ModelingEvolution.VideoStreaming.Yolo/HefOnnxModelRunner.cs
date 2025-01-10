using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using Microsoft.Extensions.Logging;
using ModelingEvolution.VideoStreaming;
using ModelingEvolution.VideoStreaming.Hailo;
using ModelingEvolution.VideoStreaming.VectorGraphics;
using Rectangle = System.Drawing.Rectangle;

namespace ModelingEvolution.VideoStreaming.Yolo;

internal class HailoModelRunner : IAsyncSegmentationModelRunner<ISegmentation>
{
    private readonly ILogger<HailoModelRunner> _logger;

    class ModelPerformanceProxy : IModelPerformance
    {
        private readonly HailoProcessor _processor;
        public ModelPerformanceProxy(HailoProcessor processor)
        {
            _processor = processor;
        }

        public TimeSpan PreProcessingTime
        {
            get
            {
                var statsWriteProcessing = _processor.Stats.WriteProcessing;
                return statsWriteProcessing.TotalProcessingTime / statsWriteProcessing.Processed;
            }
        }

        public TimeSpan InterferenceTime { get
        {
            var statsInference = _processor.Stats.ReadInterferenceProcessing;
            return statsInference.TotalProcessingTime / statsInference.Processed;
        } }

        public TimeSpan PostProcessingTime
        {
            get
            {
                var statsPostProcessing = _processor.Stats.PostProcessing;
                return statsPostProcessing.TotalProcessingTime / statsPostProcessing.Processed;
            }
        }

        public TimeSpan SignalProcessingTime
        {
            get
            {
                var c = _processor.Stats.CallbackProcessing;
                return c.TotalProcessingTime / c.Processed;
            }
        }

        public TimeSpan Total
        {
            get
            {
                var s = _processor.Stats.TotalProcessing;
                return s.TotalProcessingTime / s.Processed;
            }
        }
    }
    private static HailoProcessor _processor = null;
    

    readonly record struct Args(Rectangle Roi, Size DstSize, float Threshold);
    public HailoModelRunner(string modelFullPath, ILogger<HailoModelRunner> logger)
    {
        _logger = logger;
        _processor ??= HailoProcessor.Load(modelFullPath);
        
        _processor.FrameProcessed += OnFrameProcessed;
        Performance = new ModelPerformanceProxy(_processor);
    }

    private void OnFrameProcessed(object? sender, SegmentationResult e)
    {
        
        HailoSegmentationResult result = new HailoSegmentationResult(e)
        {
            
            DestinationSize = new Size(640,640),// for now.
            Id = e.Id
        };

        FrameSegmentationPerformed?.Invoke(this, result);
    }

    public IModelPerformance Performance { get; }
    public event EventHandler<ISegmentationResult<ISegmentation>>? FrameSegmentationPerformed;
    public unsafe void AsyncProcess(YuvFrame* frame, in Rectangle roi, in Size dstSize, float threshold)
    {
        var frameSize = frame->Info.Size;
        _processor.Confidence = threshold;
        FrameIdentifier id = new FrameIdentifier(frame->Metadata.FrameNumber, 0);
        //_logger.LogInformation($"Pending frame: {id}");
        
        _processor.WriteFrame(new IntPtr((void*)frame->Data), id, frameSize, roi, threshold);
    }

    public void StartAsync()
    {
        _processor.StartAsync();
    }


    public void Dispose()
    {
        _processor.FrameProcessed -= OnFrameProcessed;
    }
}

class HailoSegmentationResult : ISegmentationResult<ISegmentation>
{
    struct SegmentationItem(HailoSegmentationResult parent, int index) : ISegmentation
    {
        private PolygonGraphics? _polygon;
        private float? _confidence;
        public void Dispose()
        {
            _polygon?.Dispose();
        }

        private PolygonGraphics? ComputePolygon()
        {
            var points= parent._interferenceResults[index].ComputePolygonVectorU16(parent.Threshold);
            var result = new PolygonGraphics(points, parent.DestinationSize);

            return result;
        }
        
        public Mat Mask => parent._items[index].Mask;

        public PolygonGraphics? Polygon => _polygon ??= ComputePolygon();
        public SegmentationClass Name { get; init; }


        public float Confidence => parent._interferenceResults[index].Confidence;
        public Rectangle Bounds => parent._interferenceResults[index].Bbox ;
    }
    private readonly SegmentationResult _interferenceResults;
    private readonly SegmentationItem[] _items;
    private readonly int _count;
    private bool _disposed;
    public HailoSegmentationResult(SegmentationResult interferenceResults)
    {
        _interferenceResults = interferenceResults;
        _items = ArrayPool<SegmentationItem>.Shared.Rent(interferenceResults.Count);
        _count = interferenceResults.Count;
        
        for (int i = 0; i < interferenceResults.Count; i++)
            _items[i] = new SegmentationItem(this, i);
    }

    public IEnumerator<ISegmentation> GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
            yield return _items[i];
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var i in this) i.Dispose();
        ArrayPool<SegmentationItem>.Shared.Return(_items);
        _interferenceResults.Dispose();
    }

    public required Size DestinationSize { get; init; }
    public int Count => _interferenceResults.Count;
    public Rectangle Roi => this._interferenceResults.Roi;
    public float Threshold => this._interferenceResults.Threshold;
    public int UncertainCount => _interferenceResults.UncertainCount;
    public FrameIdentifier Id { get; set; }

    public ISegmentation this[int index] => _items[index];
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"DestinationSize: {DestinationSize}");
        sb.AppendLine($"Roi: {Roi}");
        sb.AppendLine($"Threshold: {Threshold}");
        sb.AppendLine($"Count: {Count}");
        foreach (var i in this)
        {
            sb.AppendLine($"Segment: {i.Name} {i.Bounds}");
        }

        return sb.ToString();
    }
}
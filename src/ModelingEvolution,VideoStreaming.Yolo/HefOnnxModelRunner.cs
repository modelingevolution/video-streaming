using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using ModelingEvolution.VideoStreaming;
using ModelingEvolution.VideoStreaming.Hailo;
using ModelingEvolution.VideoStreaming.VectorGraphics;
using Rectangle = System.Drawing.Rectangle;

namespace ModelingEvolution_VideoStreaming.Yolo;

internal class HailoModelRunner : ISegmentationModelRunner<ISegmentation>
{
    private readonly HailoProcessor _processor;
    private readonly ConcurrentDictionary<FrameIdentifier, TaskCompletionSource<SegmentationResult>> _pendingFrames;

    public HailoModelRunner(string modelFullPath)
    {
        _processor = HailoProcessor.Load(modelFullPath);
        _processor.FrameProcessed += OnFrameProcessed;
    }

    private void OnFrameProcessed(object? sender, SegmentationResult e)
    {
        if (_pendingFrames.TryRemove(e.Id, out var tcs)) 
            tcs.SetResult(e);
    }

    public ModelPerformance Performance { get; }
    public unsafe ISegmentationResult<ISegmentation> Process(YuvFrame* frame, 
        in Rectangle roi,in Size dstSize, float threshold)
    {
        var frameSize = frame->Info.Size;
        _processor.Confidence = threshold;
        FrameIdentifier id = new FrameIdentifier(frame->Metadata.FrameNumber,0);
        
        var tcs = new TaskCompletionSource<SegmentationResult>();
        _pendingFrames.TryAdd(id, tcs);

        _processor.WriteFrame(new IntPtr((void*)frame->Data), id, frameSize, roi);

        var result = tcs.Task.Result;

        return new HailoSegmentationResult(result);
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
            var sw = Stopwatch.StartNew(); sw.Start();

            var mat = Mask;
            using var threshold = new Mat();
            
            CvInvoke.Threshold(mat, threshold, parent.Threshold * 255, 255, ThresholdType.Binary);

            using var contours = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(threshold, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);

            if (contours.Size <= 0)
                return null;
            
            var largestContour = contours[0];
            for (int i = 1; i < contours.Size; i++)
                if (CvInvoke.ContourArea(contours[i]) > CvInvoke.ContourArea(largestContour))
                    largestContour = contours[i];
            
            //using var hullIndices = new VectorOfPoint();
            //CvInvoke.ConvexHull(largestContour, hullIndices);
            //var points = hullIndices.ToVectorList();
            if (largestContour.Length == 0) return null;

            var points = largestContour.ToArrayBuffer();
            Debug.Assert(points.Count > 2);

            var result = new PolygonGraphics(points, mat.Size);

            Debug.WriteLine("Compute Polygon in: " + sw.ElapsedMilliseconds);

            return result;
        }
        
        public Mat Mask => parent._items[index].Mask;

        public PolygonGraphics? Polygon => _polygon ??= ComputePolygon();
        public SegmentationClass Name { get; init; }


        public float Confidence => parent[index].Confidence;
        public Rectangle Bounds => throw new NotImplementedException();
    }
    private readonly SegmentationResult _ret;
    private readonly SegmentationItem[] _items;
    private readonly int _count;
    private bool _disposed;
    public HailoSegmentationResult(SegmentationResult ret)
    {
        _ret = ret;
        _items = ArrayPool<SegmentationItem>.Shared.Rent(ret.Count);
        _count = ret.Count;
        for (int i = 0; i < ret.Count; i++)
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
        _ret.Dispose();
    }

    public Size DestinationSize { get; init; }
    public int Count => _ret.Count;
    public Rectangle Roi { get; init; }
    public float Threshold { get; init; }

    public ISegmentation this[int index] => _items[index];
}
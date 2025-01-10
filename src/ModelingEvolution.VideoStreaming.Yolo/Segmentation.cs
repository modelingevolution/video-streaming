using System.Diagnostics;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using ModelingEvolution.VideoStreaming.VectorGraphics;
using Rectangle = System.Drawing.Rectangle;

namespace ModelingEvolution.VideoStreaming.Yolo;

public interface ISegmentation : IDisposable
{
    Mat Mask { get;  }
    
    PolygonGraphics? Polygon { get; }
    SegmentationClass Name { get; init; }
    float Confidence { get;  }
    Rectangle Bounds { get;  }
}

public class Segmentation : Detection, IYoloPrediction<Segmentation>, ISegmentation
{
    public required Mat Mask { get; init; }
    public required Rectangle Roi { get; init; }
    public required float Threshold { get; init; }
    private PolygonGraphics? _polygon;
    private bool _polygonComputed;
        
    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;
        Mask.Dispose();
        if(_polygonComputed)
            _polygon?.Dispose();
    }

    public PolygonGraphics? Polygon
    {
        get
        {
            if (_polygonComputed) return _polygon;
            _polygon = ComputePolygon();
            _polygonComputed = true;
            if(_polygon != null)
                Debug.Assert(_polygon.Polygon.Points.Count > 2);
                
            return _polygon;
        }
    }
    private PolygonGraphics? ComputePolygon()
    {
        var sw = Stopwatch.StartNew(); sw.Start();
            
        var mat = Mask;
        using var thresholded = new Mat();
        CvInvoke.Threshold(mat, thresholded, Threshold * 255, 255, ThresholdType.Binary);
            
        using var contours = new VectorOfVectorOfPoint();
        CvInvoke.FindContours(thresholded, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);
            
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
            
        var result = new PolygonGraphics(points,mat.Size);
            
        Debug.WriteLine("Compute Polygon in: " + sw.ElapsedMilliseconds);
            
        return result;
    }
        
    static string IYoloPrediction<Segmentation>.Describe(Segmentation[] predictions) => predictions.Summary();
}
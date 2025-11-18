using System.Diagnostics;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using ModelingEvolution.VideoStreaming.VectorGraphics;
using Rectangle = System.Drawing.Rectangle;

namespace ModelingEvolution.VideoStreaming.Yolo;

public interface ISegmentation : IDisposable
{
    Mat? Mask { get;  }
    
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
    private PolygonGraphics? ComputePolygonOld()
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

        // Find contour with highest average pixel value
        var highestAvgContour = contours[0];
        double highestAvgValue = CalculateAveragePixelValue(mat, contours[0]);

        for (int i = 1; i < contours.Size; i++)
        {
            double currentAvg = CalculateAveragePixelValue(mat, contours[i]);
            if (currentAvg > highestAvgValue)
            {
                highestAvgValue = currentAvg;
                highestAvgContour = contours[i];
            }
        }

        if (highestAvgContour.Length == 0) return null;

        var points = highestAvgContour.ToArrayBuffer();
        Debug.Assert(points.Count > 2);

        var result = new PolygonGraphics(points, mat.Size);

        Debug.WriteLine("Compute Polygon in: " + sw.ElapsedMilliseconds);

        return result;
    }
    private double CalculateAveragePixelValue(Mat image, VectorOfPoint contour)
    {
        // Create mask for the contour
        using var mask = new Mat(image.Size, DepthType.Cv8U, 1);
        mask.SetTo(new MCvScalar(0));

        // Fill the contour area in the mask
        using var contourVec = new VectorOfVectorOfPoint(new[] { contour });
        CvInvoke.DrawContours(mask, contourVec, 0, new MCvScalar(255), -1);

        // Calculate mean value of the original image within the contour area
        using var maskedImage = new Mat();
        CvInvoke.BitwiseAnd(image, image, maskedImage, mask);

        var mean = CvInvoke.Mean(maskedImage, mask);

        // Count non-zero pixels in mask to get the area
        var nonZeroPixels = CvInvoke.CountNonZero(mask);
        if (nonZeroPixels == 0) return 0;

        // Return average value (will be between 0 and 1 for confidence map)
        return mean.V0 / 255.0;
    }
    static string IYoloPrediction<Segmentation>.Describe(Segmentation[] predictions) => predictions.Summary();
}
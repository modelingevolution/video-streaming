using System.Diagnostics;
using System.Drawing;
using Emgu.CV.CvEnum;
using Emgu.CV;
using ModelingEvolution.Drawing;
using ModelingEvolution.VideoStreaming.Hailo;

using Rectangle = System.Drawing.Rectangle;

namespace ModelingEvolution.VideoStreaming.HailoCmd
{
    internal class Program
    {
        private static Stopwatch _sw;
        static void Main(string[] args)
        {
            string hef = args[0];
            string jpg = args[1];
            
            HailoProcessor p = HailoProcessor.Load(args[0]);
            p.FrameProcessed += OnFrameProcessed;
            p.StartAsync();
            using Mat yuvFrame = new Mat();
            Mat src = CvInvoke.Imread(jpg);
            CvInvoke.CvtColor(src, yuvFrame, ColorConversion.Bgr2YuvI420);
            
            _sw = Stopwatch.StartNew();
            p.WriteFrame(yuvFrame.DataPointer, new Size(640,640), new Rectangle(0,0,640,640));
            Console.WriteLine("Waiting for result...");
            Console.ReadLine();
        }

        private static void OnFrameProcessed(object? sender, SegmentationResult e)
        {
            Console.WriteLine($"Frame processed, found: {e.Count} objects in total: {_sw.ElapsedMilliseconds}ms.");
            Console.WriteLine($"Frame id: {e.Id.FrameId}, camera id: {e.Id.CameraId}");
            foreach (Segment segment in e)
            {
                Console.WriteLine($"Segment class-id: {segment.ClassId}, label: {segment.Label} has {segment.Confidence} confidence.");
                
                Polygon<float>? p = segment.ComputePolygon();
                if (p != null)
                {
                    var polygon = p.Value;
                    Console.WriteLine($"Segment area: {polygon.Area()} in {polygon.BoundingBox()}");
                    Console.WriteLine($"Bbox: {segment.Bbox} resolution: {segment.Resolution}");
                }
            }
        }
    }
}

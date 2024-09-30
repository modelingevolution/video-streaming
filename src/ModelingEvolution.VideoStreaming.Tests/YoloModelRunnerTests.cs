using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using ModelingEvolution_VideoStreaming.Yolo;
using ModelingEvolution.VideoStreaming.Buffers;

namespace ModelingEvolution.VideoStreaming.Tests
{
    public class YoloModelRunnerTests
    {
        [Fact]
        public void YuvConvert()
        {
            var frame = FrameLoader.Load("sports-resized.jpg");
            var mat = frame.ToMatFrame();
            mat.Data.Save("sports-resized.out.jpg");
        }
        [Fact]
        public unsafe void Process()
        {
            var runner = YoloModelFactory.LoadSegmentationModel("yolov8n-seg-uint8.onnx");

            var frame = FrameLoader.Load("sports-resized.jpg");
            var rect = new Rectangle(0, 0, 640, 640);
            runner.Process(&frame, &rect);
        }
        
        
    }

    public static class FrameLoader
    {
        static Mat tmp;
        public unsafe static YuvFrame Load(string imgFile)
        {
            var mat = new Mat(imgFile);
            var dst = tmp = new Mat();
            
            CvInvoke.CvtColor(mat, dst, ColorConversion.Bgr2YuvI420);
            Debug.WriteLine($"Stride: {dst.Step}, width: {dst.Width}");
            var s = (ulong)(mat.Width * mat.Height);
            s = s + s / 2;

            
            var metadata = new FrameMetadata(0, s, 0);
            var info = new FrameInfo(mat.Width, mat.Height, mat.Width);
            byte* ptr = (byte*)tmp.DataPointer;
            byte p = ptr[0];
            return new YuvFrame(metadata,info,(byte*)dst.GetDataPointer());
        }
    }
}

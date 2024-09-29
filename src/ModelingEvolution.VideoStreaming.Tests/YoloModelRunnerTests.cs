using System;
using System.Collections.Generic;
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
        public unsafe void Process()
        {
            var runner = YoloModelFactory.LoadSegmentationModel("yolov8n-seg-uint8.onnx");

            var frame = FrameLoader.Load("sports.jpg");
            var rect = new Rectangle(0, 0, frame.Info.Width, frame.Info.Height);
            runner.Process(&frame, &rect);
        }
        
        
    }

    public static class FrameLoader
    {
        public unsafe static YuvFrame Load(string imgFile)
        {
            var mat = new Mat(imgFile);
            CvInvoke.CvtColor(mat, mat, ColorConversion.Bgr2Yuv);

            var s = (ulong)(mat.Width * mat.Height);
            s = s + s / 2;

            
            var metadata = new FrameMetadata(0, s, 0);
            var info = new FrameInfo(mat.Width, mat.Height, mat.Width);
            
            return new YuvFrame(metadata,info,(byte*)mat.DataPointer);
        }
    }
}

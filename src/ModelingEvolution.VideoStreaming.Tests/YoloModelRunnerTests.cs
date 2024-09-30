﻿using System;
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
using Xunit.Abstractions;

namespace ModelingEvolution.VideoStreaming.Tests
{
    public class YoloModelRunnerTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public YoloModelRunnerTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

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
            string fileName = "sports-resized.jpg";
            var runner = YoloModelFactory.LoadSegmentationModel("yolov8n-seg-uint8.onnx");

            var frame = FrameLoader.Load(fileName);
            var rect = new Rectangle(0, 0, 640, 640);
            var result = runner.Process(&frame, &rect);
            StringBuilder sb = new StringBuilder();
            SizeF scale = new SizeF(1.0f / rect.Width, 1.0f / rect.Height);
            foreach (var i in result)
            {
                sb.AppendLine($"{i.Name.Id} {i.ComputePolygon(0.6f)
                    .ToPolygonF().ScaleBy(scale).ToAnnotationString()}");
            }

            var annotatedFile = Path.GetFileNameWithoutExtension(fileName) + ".txt";
            File.WriteAllText(annotatedFile, sb.ToString());
            
            File.Copy(fileName, "1.1.jpg",true);
            File.Copy(annotatedFile, "1.1.txt",true);
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

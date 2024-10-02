using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using FluentAssertions;
using ModelingEvolution_VideoStreaming.Yolo;
using ModelingEvolution.Drawing;
using ModelingEvolution.VideoStreaming.Buffers;
using ModelingEvolution.VideoStreaming.VectorGraphics;
using Xunit.Abstractions;
using Rectangle = System.Drawing.Rectangle;
using Serializer = ProtoBuf.Serializer;

namespace ModelingEvolution.VideoStreaming.Tests
{
    public class SerializationTests
    {
        [Fact]
        public void PolygonCanBeSerialized()
        {
            Polygon tmp = new Polygon(new VectorU16[] { new VectorU16(1, 1) });
            tmp.Points.Should().HaveCount(1);
            ArrayBufferWriter<byte> writer = new ArrayBufferWriter<byte>();
            Serializer.Serialize(writer, tmp);
            var tmp2 = Serializer.Deserialize<Polygon>(writer.WrittenMemory);
            tmp2.Points.Should().BeEquivalentTo(tmp.Points);
        }
        [Fact]
        public void PolygonCanBeSerializedWithManyPoints()
        {
            Polygon tmp = Polygon.GenerateRandom(100);
            tmp.Points.Should().HaveCount(100);
            ArrayBufferWriter<byte> writer = new ArrayBufferWriter<byte>();
            Serializer.Serialize(writer, tmp);
            var tmp2 = Serializer.Deserialize<Polygon>(writer.WrittenMemory);
            tmp2.Points.Should().BeEquivalentTo(tmp.Points);
        }
    }
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
           
            var sb = RunAndGetAnnotations(fileName, "yolov8n-seg-uint8.onnx");
            sb = RunAndGetAnnotations(fileName, "yolov8n-seg-uint8.onnx");
            sb = RunAndGetAnnotations(fileName, "yolov8n-seg-uint8.onnx");

            var annotatedFile = Path.GetFileNameWithoutExtension(fileName) + ".txt";
            File.WriteAllText(annotatedFile, sb.ToString());
            
            File.Copy(fileName, "1.1.jpg",true);
            File.Copy(annotatedFile, "1.1.txt",true);
        }

        private unsafe StringBuilder RunAndGetAnnotations(string fileName, string modelPath)
        {
            var runner = YoloModelFactory.LoadSegmentationModel(modelPath);

            var frame = FrameLoader.Load(fileName);
            var rect = new Rectangle(0, 0, 640, 640);
            using var result = runner.Process(&frame, &rect, 0.6f);
            StringBuilder sb = new StringBuilder();
            
            foreach (var i in result)
            {
                var polygon = i.Polygon;
                if (polygon == null) continue;
                
                sb.AppendLine($"{i.Name.Id} {polygon.NormalizedPolygon().ToAnnotationString()}");
            }
            this._testOutputHelper.WriteLine($"{runner.Performance}");
            return sb;
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

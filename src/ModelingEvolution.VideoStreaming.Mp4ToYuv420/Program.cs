using OpenCvSharp;
using System.Runtime.InteropServices;

namespace ModelingEvolution.VideoStreaming.Mp4ToYuv420
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string src = args[0];
            if(!File.Exists(src)) 
            {
                Console.WriteLine("File not found: " + src);
                return;
            }
            string dst = args.Length == 1 ? src+".yuv" : args[1];
            //SaveYuvToJpeg(dst);
            ExtractFramesToYUV420(src, dst);
        }
        private static void ExtractFramesToYUV420(string src, string dst)
        {
            using var capture = new VideoCapture(src);
            if (!capture.IsOpened())
            {
                throw new InvalidOperationException("Failed to open video file.");
            }

            int frameWidth = (int)capture.FrameWidth;
            int frameHeight = (int)capture.FrameHeight;
            int count = capture.FrameCount;
            using Mat frame = new Mat();
            using Mat yuvFrame = new Mat();
            int c = 0;
            while (true)
            {
                bool isSuccess = capture.Read(frame);
                if (!isSuccess || frame.Empty()) break;

                Cv2.CvtColor(frame, yuvFrame, ColorConversionCodes.BGR2YUV_I420);
                SaveYUV420Frame(yuvFrame, frameWidth, frameHeight, dst);
                Console.Write($"\r{c*100/count}% ({c++})");
            }
        }

        private static void SaveYuvToJpeg(string file)
        {
            byte[] data = File.ReadAllBytes(file);
            int h = 1080;
            int w = 1920;
            Mat m = new Mat(h, w, MatType.CV_8U, data, 0);
            m.SaveImage($"{file}.jpg");
        }

        private static void SaveYUV420Frame(Mat frame, int width, int height, string outputPath)
        {
            // Calculate the size of the YUV 420 frame
            int ySize = width * height;
            int uvSize = (width / 2) * (height / 2);
            int frameSize = ySize + 2 * uvSize;

            // Ensure the output directory exists
            string outputDir = Path.GetDirectoryName(outputPath);
            if(!string.IsNullOrEmpty(outputDir)) 
                if (!Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);
            

            // Write the raw YUV data to a file
            using FileStream fs = new FileStream(outputPath, FileMode.Append);
            byte[] data = new byte[frameSize];
            var size = frame.Total() * frame.ElemSize();
            Marshal.Copy(frame.Data, data, 0, frameSize);
            fs.Write(data, 0, data.Length);
        }
    }
}

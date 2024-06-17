using Emgu.CV.CvEnum;
using Emgu.CV;
using System.Diagnostics;
using EventPi.SharedMemory;

namespace ModelingEvolution.VideoStreaming.CameraSimulator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Args: [mp4 file] [stream name]");
                return;
            }
            string src = args[0];
            string streamName = args[1];
            double fps = 30;
            if (!File.Exists(src))
            {
                Console.WriteLine("File not found: " + src);
                return;
            }

            var capture = new VideoCapture(src);
            if (!capture.IsOpened)
            {
                throw new InvalidOperationException("Failed to open video file.");
            }

            int frameWidth = (int)capture.Width;
            int frameHeight = (int)capture.Height;
            ;
            using Mat frame = new Mat();
            using var yuvFrame = new Mat();
            int c = 0;
            
            var frameSize = (int)(frameWidth * frameHeight * 1.5);

            Stopwatch sw = new Stopwatch();
            sw.Start();
            int loop = 0;
            var dt = TimeSpan.FromSeconds(1.0 / fps);
            SharedCyclicBuffer buffer = new SharedCyclicBuffer(120, frameSize, streamName);
            while (true)
            {
                DateTime s = DateTime.Now;
                bool isSuccess = capture.Read(frame);
                if (!isSuccess)
                {
                    capture.Dispose();
                    capture = new VideoCapture(src);
                    Console.WriteLine($"\nLoop: {++loop}");
                    continue;
                }
                
                CvInvoke.CvtColor(frame, yuvFrame, Emgu.CV.CvEnum.ColorConversion.Bgr2YCrCb);
                buffer.PushBytes(yuvFrame.DataPointer);    
                c++;

                if (c % 15 == 0) 
                    Console.Write($"\r{c}, fps: {(c/sw.Elapsed.TotalSeconds):N0} ");

                var d = dt - (DateTime.Now - s);
                if (d.TotalMilliseconds > 0)
                    Thread.Sleep(d);
            }
            capture.Dispose();
            sw.Stop();
        }
    }
}

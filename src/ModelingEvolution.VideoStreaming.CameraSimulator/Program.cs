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
            if (args.Length < 2)
            {
                Console.WriteLine("Args: [mp4 file] [stream name]");
                return;
            }

            bool interactive = args.Contains("-interactive") || args.Contains("-i");
            bool wait = args.Contains("-wait");
           
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
                throw new InvalidOperationException("Failed to open video file.");

            int frameWidth = (int)capture.Width;
            int frameHeight = (int)capture.Height;
            ;
            using Mat frame = new Mat();
            List<Mat> decodedFrames = new List<Mat>();
            while (true)
            {
                DateTime s = DateTime.Now;
                bool isSuccess = capture.Read(frame);
                if (!isSuccess) break;
                if (frame.IsEmpty) continue;

                var yuvFrame = new Mat();
                CvInvoke.CvtColor(frame, yuvFrame, Emgu.CV.CvEnum.ColorConversion.Bgr2YuvI420);
                decodedFrames.Add(yuvFrame);
            }
            Console.WriteLine("Movie decoded.");
            if (wait)
            {
                Console.WriteLine("Press a key to continue...");
                Console.ReadKey();
            }

            int c = 0;
            
            var frameSize = (int)(frameWidth * frameHeight * 1.5);

            Stopwatch sw = new Stopwatch();
            sw.Start();
            int loop = 0;
            int moveByCounter = -1;
            var dt = TimeSpan.FromSeconds(1.0 / fps);
            Console.WriteLine($"Shared memory: {streamName}, total size: {frameSize * 120} bytes, frame: {frameSize} bytes");
            SharedCyclicBuffer buffer = new SharedCyclicBuffer(120, frameSize, streamName);
            buffer.Clear();
            while (true)
            {
                foreach (var yuvFrame in decodedFrames)
                {
                   
                    DateTime s = DateTime.Now;
                    buffer.PushBytes(yuvFrame.DataPointer);
                    c++;

                    if (c % 15 == 0 || interactive)
                        Console.Write($"\r{c}, fps: {(c / sw.Elapsed.TotalSeconds):N0} ");

                    while (true)
                    {
                        var d = dt - (DateTime.Now - s);
                        if (d.TotalMilliseconds > 0)
                            Thread.SpinWait(100);
                        else break;
                    }

                    if (interactive && --moveByCounter < 0)
                    {
                        Console.WriteLine("\nPress ENTER to push next frame.");
                        string l = Console.ReadLine();
                        if (int.TryParse(l, out var moveBy))
                        {
                            moveByCounter = moveBy;
                        }
                    }
                }
                Console.WriteLine($"\nLoop: {loop++}");
            }
            capture.Dispose();
            sw.Stop();
        }
    }
}

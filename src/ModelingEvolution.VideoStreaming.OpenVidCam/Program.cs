using Microsoft.Extensions.Configuration;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using EventPi.SharedMemory;
using OpenMode = Microsoft.VisualBasic.OpenMode;

namespace ModelingEvolution.VideoStreaming.OpenVidCam
{
    internal class Program
    {
        static void Main(string[] args)
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json",
                    optional: true)
                .AddJsonFile($"appsettings.override.json", optional: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            bool shouldExit = false;
            Console.CancelKeyPress += new ConsoleCancelEventHandler((s,e) => shouldExit = true);
            
            var width = configuration.Width();
            var height = configuration.Height();
            var display = configuration.Display();
            var cameraNr = configuration.CameraNr();
            var streamName = configuration.StreamName();
            
            Console.WriteLine("OpenVidCam with OpenCV.");
            Console.WriteLine($"Camera: {cameraNr}, Resolution: {width}x{height}, Display: {display}, Shm: {streamName}");
            
            using VideoCapture capture = new VideoCapture(cameraNr);
            
            capture.Set(CapProp.FrameWidth, width);
            capture.Set(CapProp.FrameHeight, height);
            capture.Set(CapProp.Fps, configuration.Fps());
            capture.Start();
            
            
            
            Mat frame = new Mat();
            var frameSize = (int)(width * height * 1.5);
            
            using SharedCyclicBuffer buffer = new SharedCyclicBuffer(120, frameSize, streamName, EventPi.SharedMemory.OpenMode.OpenExistingForWriting);
            Console.WriteLine("Shared buffer opened.");
            
            FpsWatch fps = new FpsWatch();
            PeriodicConsoleWriter console = new PeriodicConsoleWriter(TimeSpan.FromSeconds(5));

            var size = new System.Drawing.Size(width, height);
            if(capture.Width != size.Width || capture.Height != size.Height)
                Console.WriteLine($"OpenVidCam will resize image. Original camera resolution is: {capture.Width}x{capture.Height}");
            
            while (!shouldExit)
            {
                if (!capture.Read(frame) || frame.IsEmpty)
                {
                    Thread.SpinWait(200);
                    continue;
                }

                fps++;
                console.WriteLine($"Fps: {fps.ToString()}");
                // Optionally resize & convert to YUV420
                Mat src = frame;

                if (frame.Width != width || frame.Height != height)
                {
                    src = new Mat();
                    CvInvoke.Resize(frame, src, size);
                }


                using Mat yuvFrame = new Mat();
                CvInvoke.CvtColor(src, yuvFrame, ColorConversion.Bgr2YuvI420);
                
                if (src != frame)
                    src.Dispose();
                
                buffer.PushBytes(yuvFrame.DataPointer);


                if (display)
                {
                    CvInvoke.Imshow($"Cam {cameraNr}", frame);
                    if (CvInvoke.WaitKey(30) >= 0)
                        break;
                }
                
            }
        }
    }
}

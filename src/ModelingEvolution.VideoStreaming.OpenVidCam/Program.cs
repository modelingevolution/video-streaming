using System.Buffers;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using EventPi.SharedMemory;
using OpenMode = Microsoft.VisualBasic.OpenMode;
using System.Text;
using System.Threading.Channels;
using System.Xml.Serialization;
using ModelingEvolution.VideoStreaming.Player;
using System.Collections;
using System.Diagnostics;

namespace ModelingEvolution.VideoStreaming.OpenVidCam
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                await Run(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.Error.WriteLine(ex.Message);
                Console.WriteLine("Exiting...");
            }
        }

        private static async Task Run(string[] args)
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

            var remoteHost = configuration.RemoteHost();
            Console.WriteLine("OpenVidCam with OpenCV.");

            if (string.IsNullOrWhiteSpace(remoteHost))
            {
                Console.WriteLine(
                    $"Camera: {cameraNr}, Resolution: {width}x{height}, Display: {display}, Shm: {streamName}");

                using VideoCapture capture = new VideoCapture(cameraNr);

                capture.Set(CapProp.FrameWidth, width);
                capture.Set(CapProp.FrameHeight, height);
                capture.Set(CapProp.Fps, configuration.Fps());
                capture.Start();


                Mat frame = new Mat();
                var frameSize = (int)(width * height * 1.5);

                using SharedCyclicBuffer buffer = new SharedCyclicBuffer(120, frameSize, streamName,
                    EventPi.SharedMemory.OpenMode.OpenExistingForWriting);
                Console.WriteLine("Shared buffer opened.");

                FpsWatch fps = new FpsWatch();
                PeriodicConsoleWriter console = new PeriodicConsoleWriter(TimeSpan.FromSeconds(5));

                var size = new System.Drawing.Size(width, height);
                if (capture.Width != size.Width || capture.Height != size.Height)
                    Console.WriteLine(
                        $"OpenVidCam will resize image. Original camera resolution is: {capture.Width}x{capture.Height}");

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
            else
            {
                var remotePort = configuration.RemotePort();
                var frameSize = (int)(width * height * 1.5);
                Console.WriteLine($"Resolution: {width}x{height}, Display: {display}, Shm: {streamName}, Remote host: {remoteHost}, Remote port: {remotePort}");
                SharedBufferWriter sw = new SharedBufferWriter(frameSize, streamName);

                try
                {
                    Console.WriteLine("Trying to connect...");
                    using TcpClient tcp = new TcpClient(remoteHost, remotePort);
                    var stream = tcp.GetStream();
                    Console.WriteLine("Connected.");
                    await stream.WritePrefixedAsciiString(streamName);
                    Console.WriteLine("Handshake completed. Beginning pure frame transfer...");

                    await stream.CopyAsync(sw.Write);
                }
                catch(Exception ex) 
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine("Waiting 5 seconds to reconnect.");
                    await Task.Delay(5000);
                }
            }
        }
    }

    class SharedBufferWriter : IDisposable
    {
        
        private FpsWatch _fps;
        private readonly PeriodicConsoleWriter _console;
        private readonly SharedCyclicBuffer _destinationBuffer;
        private ulong _count = 0;
        public SharedBufferWriter(int frameSize, string streamName)
        {
            this._destinationBuffer = new SharedCyclicBuffer(120, frameSize, streamName,
                EventPi.SharedMemory.OpenMode.OpenExistingForWriting);
            Console.WriteLine("Shared buffer opened.");

            this._fps = new FpsWatch();
            this._console = new PeriodicConsoleWriter(TimeSpan.FromSeconds(5));
        }

        public void Write(JpegFrame frame)
        {
            var sw = Stopwatch.StartNew();
            var jpegData = frame.Data;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(frame.Data.Length);
            
            jpegData.CopyTo(buffer);
            var left = buffer.Length - jpegData.Length;
            if(left > 0)
                Array.Clear(buffer, jpegData.Length, left);
            
            // load data to Mat.
            using Mat srcJpeg = new Mat();
            CvInvoke.Imdecode(buffer, ImreadModes.Color, srcJpeg);
            // Convert Mat to Yuv420
            using Mat yuvMat = new Mat();
            CvInvoke.CvtColor(srcJpeg, yuvMat, ColorConversion.Bgr2YuvI420);

            // Write data to destination buffer
            _destinationBuffer.PushBytes(yuvMat.DataPointer);
            ArrayPool<byte>.Shared.Return(buffer);
            
            _count += 1;
            _fps++;
            _console.WriteLine($"fps: {_fps}, encoded in: {(int)sw.Elapsed.TotalMilliseconds} ms, frames written: {_count}");
        }

        public void Dispose()
        {
            _destinationBuffer.Dispose();
        }
    }
}

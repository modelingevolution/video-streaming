using System.Runtime.InteropServices;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using OpenCvSharp;

namespace ModelingEvolution.VideoStreaming.Mp4ToMjpeg
{

    public static class JpegEncoderFactory
    {
        private static bool _initialized = false;
        public static JpegEncoder Create(int width, int height, int quality, ulong minimumBufferSize)
        {
            if (!_initialized)
            {
                var currentDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string? srcDir = null;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    srcDir = Path.Combine(currentDir, "libs", "win");
                else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    var arch = RuntimeInformation.OSArchitecture.ToString().ToLower();
                    srcDir = Path.Combine(currentDir, "libs", $"linux-{arch}");
                } 
                else throw new PlatformNotSupportedException();
                if(Directory.Exists(srcDir))
                foreach (var i in Directory.GetFiles(srcDir))
                {
                    var dst = Path.Combine(currentDir, Path.GetFileName(i));
                    if (!File.Exists(dst))
                        File.Copy(i, dst.Replace(".so",".dll"));
                }

                _initialized = true;
            }
            return new JpegEncoder(width, height, quality, minimumBufferSize);
        }
    }
    public class JpegEncoder : IDisposable
    {

        private readonly IntPtr _encoderPtr;
        private bool _disposed;
        [DllImport("LibJpegWrap.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr Create(int width, int height, int quality, ulong bufSize);

        [DllImport("LibJpegWrap.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "Encode")]
        private static extern ulong OnEncode(IntPtr encoder, nint data, nint dstBuffer, ulong dstBufferSize);

        [DllImport("LibJpegWrap.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Close(IntPtr encoder);

        public JpegEncoder(int width, int height, int quality, ulong minimumBufferSize)
        {
            _encoderPtr = Create(width, height, quality, minimumBufferSize);
        }

       
        public ulong Encode(nint data, nint dst, ulong dstBufferSize)
        {
            return OnEncode(_encoderPtr, data,dst, dstBufferSize);
        }
       
        public unsafe ulong Encode(nint data, byte[] dst)
        {
            if (_encoderPtr != IntPtr.Zero)
                fixed (byte* dstPtr = dst)
                {
                    return OnEncode(_encoderPtr, data, (nint)dstPtr, (ulong)dst.LongLength);
                }
            return 0;
        }
        public unsafe ulong Encode(byte[] data, byte[] dst)
        {
            if (_encoderPtr != IntPtr.Zero)
                // Pin the byte[] array
                fixed (byte* p = data)
                fixed (byte* dstPtr = dst)
                {
                    return OnEncode(_encoderPtr, (nint)p, (nint)dstPtr, (ulong)dst.LongLength);
                }

            return 0;
        }
        ~JpegEncoder()
        {
            Dispose();
        }
        public void Dispose()
        {
            if (_encoderPtr == IntPtr.Zero || _disposed) return;
            Close(_encoderPtr);
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            string src = args[0];
            if (!File.Exists(src))
            {
                Console.WriteLine("File not found: " + src);
                return;
            }

            string dst = args.Length == 1 ? src + ".yuv" : args[1];
            //SaveYuvToJpeg(dst);
            ExtractFramesToJpegInMem(src, dst);
        }
        private static void ExtractFramesToJpegInMem(string src, string dst)
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
            List<Mat> yuvFrames = new();
            int c = 0;
            ulong bufferSize = (ulong)(frameWidth * frameHeight * 1.5);
            using JpegEncoder encoder = new JpegEncoder(frameWidth, frameHeight, 90, bufferSize);

            Stopwatch sw = new Stopwatch();
            sw.Start();
            while (true)
            {
                bool isSuccess = capture.Read(frame);
                if (!isSuccess || frame.Empty()) break;
                var yuvFrame = new Mat();
                yuvFrames.Add(yuvFrame);
                Cv2.CvtColor(frame, yuvFrame, ColorConversionCodes.BGR2YUV_I420);
                //SaveYUV420Frame(yuvFrame, frameWidth, frameHeight, dst);
                //File.WriteAllBytes($"{c}.jpeg",encoder.GetOutputBuffer());
                //Console.Write($"\r{c * 100 / count}% ({c++})");
                c++;
            }
            sw.Stop();
            Console.WriteLine("Load from disk:");
            Console.WriteLine($"Elapsed: {sw.Elapsed}");
            Console.WriteLine($"Fps: {c / sw.Elapsed.TotalSeconds}");
            sw.Restart();
            c = 0;
            byte[] dstBuffer = new byte[bufferSize];
            BigInteger totalSize = 0;
            foreach(var i in yuvFrames)
            {
                var size = encoder.Encode(i.DataStart, dstBuffer);
                totalSize += size;
                c++;

                //sw.Stop();
                //using var fs = new FileStream($"{c}.jpeg", FileMode.Create);
                //fs.Write(dstBuffer, 0, (int)size);

                //Console.Write($"\r{c * 100 / count}% ({c})");
                //sw.Start();
            }
            sw.Stop();
            var megapixels = (BigInteger)frameWidth * (BigInteger)frameHeight * (BigInteger)c;
            const ulong mb = 1024 * 1024;
            Console.WriteLine("Jpeg convert:");
            Console.WriteLine($"Elapsed      : {sw.Elapsed}");
            Console.WriteLine($"In total     : {megapixels/ mb} MB");
            Console.WriteLine($"Out total    : {totalSize/ mb} MB");
            Console.WriteLine($"Frames / sec : {c / sw.Elapsed.TotalSeconds}");
            Console.WriteLine($"MegaPixels /s: {megapixels / (BigInteger)sw.Elapsed.TotalSeconds / 1000000}");
            Console.WriteLine($"Jpeg MB/sec  : {totalSize / (BigInteger)sw.Elapsed.TotalSeconds / mb}");
            Console.ReadLine();
        }
        private static void ExtractFramesToJpeg(string src, string dst)
        {
            using var capture = new VideoCapture(src);
            if (!capture.IsOpened())
            {
                throw new InvalidOperationException("Failed to open video file.");
            }

            int frameWidth = (int)capture.FrameWidth;
            int frameHeight = (int)capture.FrameHeight;
            ulong bufferSize = (ulong)(frameWidth * frameHeight * 1.5);
            int count = capture.FrameCount;
            using Mat frame = new Mat();
            using Mat yuvFrame = new Mat();
            int c = 0;

            using JpegEncoder encoder = new JpegEncoder(frameWidth, frameHeight, 90, bufferSize);

            Stopwatch sw = new Stopwatch();
            sw.Start();
            byte[] dstBuffer = new byte[bufferSize];
            while (true)
            {
                bool isSuccess = capture.Read(frame);
                if (!isSuccess || frame.Empty()) break;

                Cv2.CvtColor(frame, yuvFrame, ColorConversionCodes.BGR2YUV_I420);
                //SaveYUV420Frame(yuvFrame, frameWidth, frameHeight, dst);
                encoder.Encode(yuvFrame.DataStart, dstBuffer);
                //File.WriteAllBytes($"{c}.jpeg",encoder.GetOutputBuffer());
                Console.Write($"\r{c * 100 / count}% ({c++})");
            }
            sw.Stop();
            Console.WriteLine($"Elapsed: {sw.Elapsed}");
            Console.WriteLine($"Fps: {c/sw.Elapsed.TotalSeconds}");
        }

       

        private static void SaveYUV420Frame(Mat frame, int width, int height, string outputPath)
        {
            // Calculate the size of the YUV 420 frame
            int ySize = width * height;
            int uvSize = (width / 2) * (height / 2);
            int frameSize = ySize + 2 * uvSize;

            // Ensure the output directory exists
            string outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
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

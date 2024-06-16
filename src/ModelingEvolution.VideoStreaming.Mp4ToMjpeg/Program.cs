using System.Runtime.InteropServices;
using System;
using System.Diagnostics;
using OpenCvSharp;

namespace ModelingEvolution.VideoStreaming.Mp4ToMjpeg
{
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct YuvEncoder
    {
        public byte* input_buffer;
        //public byte* output_buffer;
        public ulong* output_buffer_size;
        public ulong* output_frame_size;
    }
    public unsafe class JpegEncoder : IDisposable
    {
        private readonly YuvEncoder* _encoderPtr;
        private bool _disposed;
        [DllImport("LibJpegWrap.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern YuvEncoder* Create(int width, int height, int quality, ulong bufSize);

        [DllImport("LibJpegWrap.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "Encode")]
        private static extern ulong OnEncode(YuvEncoder* encoder, nint data, nint dstBuffer, ulong dstBufferSize);

        [DllImport("LibJpegWrap.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Close(YuvEncoder* encoder);

        public JpegEncoder(int width, int height, int quality, ulong minimumBufferSize)
        {
            _encoderPtr = Create(width, height, quality, minimumBufferSize);
        }

        public byte[] GetOutputBuffer()
        {
            byte[] buffer = new byte[*_encoderPtr->output_buffer_size];
            //Marshal.Copy((IntPtr)_encoderPtr->output_buffer, buffer, 0, buffer.Length);
            return buffer;
        }
        public ulong Encode(nint data, nint dst, ulong dstBufferSize)
        {
            return OnEncode(_encoderPtr, data,dst, dstBufferSize);
        }
        private ulong OutputBufferSize => *(_encoderPtr->output_buffer_size);
        public ulong Encode(nint data, byte[] dst)
        {
            //if ((ulong)dst.LongLength < OutputBufferSize)
            //    throw new ArgumentException("Destination buffer size must be equal or bigger to output buffer size");

            if (_encoderPtr != null)
                // Pin the byte[] array
                fixed (byte* dstPtr = dst)
                {
                    return OnEncode(_encoderPtr, data, (nint)dstPtr, (ulong)dst.LongLength);
                }
            return 0;
        }
        public ulong Encode(byte[] data, byte[] dst)
        {
            if((ulong)dst.LongLength < OutputBufferSize)
                throw new ArgumentException("Destination buffer size must be equal or bigger to output buffer size");

            if (_encoderPtr != null)
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
            if (_encoderPtr == null || _disposed) return;
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
            foreach(var i in yuvFrames)
            {
                var size = encoder.Encode(i.DataStart, dstBuffer);

                sw.Stop();
                using var fs = new FileStream($"{c}.jpeg", FileMode.Create);
                fs.Write(dstBuffer, 0, (int)size);
                c++;
                Console.Write($"\r{c * 100 / count}% ({c})");
                sw.Start();
            }
            sw.Stop();
            Console.WriteLine("Jpeg convert:");
            Console.WriteLine($"Elapsed: {sw.Elapsed}");
            Console.WriteLine($"Fps: {c / sw.Elapsed.TotalSeconds}");
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

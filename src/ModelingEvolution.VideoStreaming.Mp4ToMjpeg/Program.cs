using System;
using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using ModelingEvolution.VideoStreaming.LibJpegTurbo;


namespace ModelingEvolution.VideoStreaming.Mp4ToMjpeg
{
    static class FileExtensions
    {
        public static void WriteBytes(string path, byte[] data, int size)
        {
            using var fs = new FileStream(path, FileMode.Create);
                fs.Write(data, 0, size);
        }
    }
    public class CyclicMemoryBuffer
    {
        private readonly uint _maxObjectSize;
        private readonly Memory<byte> _buffer;
        private readonly MemoryHandle _handle;
        private uint _cursor;
        private ulong _cycle;
        private ulong _written;
        public long Size => _buffer.Length;
        public ulong MaxObjectSize => _maxObjectSize;
        public CyclicMemoryBuffer(uint capacity, uint maxObjectSize)
        {
            _buffer = new Memory<byte>(new byte[capacity * maxObjectSize]);
            _handle = _buffer.Pin();
            _maxObjectSize = maxObjectSize;
        }
        public void Write<T>(in T data, uint offset = 0) where T : struct
        {
            MemoryMarshal.Write(_buffer.Span.Slice((int)(_cursor + offset)), in data);
        }
        public ulong SpaceLeft => (ulong)(_buffer.Length - _cursor);
        public unsafe byte* GetPtr(int offset = 0)
        {
            return ((byte*)_handle.Pointer) + _cursor + offset;
        }
        public Memory<byte> Use(uint size)
        {
            var u = _buffer.Slice((int)_cursor, (int)size);
            Advance(size);
            return u;
        }
        /// <summary>
        /// Advances the specified size.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns>true if it is the next cycle in the buffer.</returns>
        public bool Advance(uint size)
        {
            bool nextIteration = false;
            var left = _buffer.Length - _cursor;
            if (left <= (_maxObjectSize))
            {
                _cursor = 0;
                _written += (ulong)size;
                nextIteration = true;
                Interlocked.Increment(ref _cycle);
                Debug.WriteLine($"Buffer is full for writing, resetting. Last frame was: {_cycle - 1} ");
            }
            else
            {
                _cursor += size;
                _written += (ulong)size;
            }
            return nextIteration;
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
            //ExtractFramesToJpeg(src, dst);
            ExtractFramesToJpegInMem(src, dst);
            Console.ReadLine();
        }
        private static void ExtractFramesToJpegInMem(string src, string dst)
        {
            using var capture = new VideoCapture(src);
            if (!capture.IsOpened)
            {
                throw new InvalidOperationException("Failed to open video file.");
            }

            int frameWidth = (int)capture.Width;
            int frameHeight = (int)capture.Height;
            ;
            using Mat frame = new Mat();
            List<Mat> yuvFrames = new();
            int c = 0;
            double count = capture.Get(CapProp.FrameCount);
            ulong bufferSize = (ulong)(frameWidth * frameHeight * 1.5);

            
            Stopwatch sw = new Stopwatch();
            sw.Start();
            var ic = 0;
            while (true)
            {
                bool isSuccess = capture.Read(frame);
                if (!isSuccess || ic++ >= 2*120) break;
                var yuvFrame = new Mat();
                yuvFrames.Add(yuvFrame);

                CvInvoke.CvtColor(frame, yuvFrame, Emgu.CV.CvEnum.ColorConversion.Bgr2YuvI420);
                //Cv2.CvtColor(frame, yuvFrame, ColorConversionCodes.BGR2YUV_I420);

                //SaveYUV420Frame(yuvFrame, frameWidth, frameHeight, dst);
                //File.WriteAllBytes($"{c}.jpeg",encoder.GetOutputBuffer());
                //Console.Write($"\r{c * 100 / count}% ({c++})");
                c++;
            }
            sw.Stop();
            GC.Collect();
            Console.WriteLine("Load from disk:");
            Console.WriteLine($"Elapsed: {sw.Elapsed}");
            Console.WriteLine($"Fps: {c / sw.Elapsed.TotalSeconds}");
            Console.WriteLine();
            Encode(sw, bufferSize, yuvFrames, frameWidth, frameHeight, 75, DiscreteCosineTransform.Integer);
            Encode(sw, bufferSize, yuvFrames, frameWidth, frameHeight, 90, DiscreteCosineTransform.Integer);
            Encode(sw, bufferSize, yuvFrames, frameWidth, frameHeight, 95, DiscreteCosineTransform.Integer);

            Encode(sw, bufferSize, yuvFrames, frameWidth, frameHeight, 75, DiscreteCosineTransform.Float);
            Encode(sw, bufferSize, yuvFrames, frameWidth, frameHeight, 90, DiscreteCosineTransform.Float);
            Encode(sw, bufferSize, yuvFrames, frameWidth, frameHeight, 95, DiscreteCosineTransform.Float);
        }

        private static unsafe void Encode(Stopwatch sw, ulong bufferSize, List<Mat> yuvFrames,
            int frameWidth,
            int frameHeight, 
            int quality, 
            DiscreteCosineTransform mode)
        {
            using JpegEncoder encoder = JpegEncoderFactory.Create(frameWidth, frameHeight, quality, bufferSize);
            encoder.Quality = quality;
            encoder.Mode = mode;
            int c;
            sw.Restart();
            c = 0;
            byte[] dstBuffer = new byte[bufferSize];
            BigInteger totalSize = 0;
            var buffer = new CyclicMemoryBuffer(4, 1920*1080*3/2);

            while(true)
            foreach (var i in yuvFrames)
            {
                var dst = buffer.GetPtr();
                var size = encoder.Encode(i.DataPointer,(nint)dst, buffer.MaxObjectSize);
                var data = buffer.Use((uint)size);

                totalSize += size;
                c++;

                if(c % 30 == 0)
                    {
                        Console.WriteLine($"\r{c}");
                    }
                //sw.Stop();
                //string fn = $"{c}.jpeg";
                //if (File.Exists(fn)) File.Delete(fn);
                //using (var fs = new FileStream(fn, FileMode.Create))
                //{
                //    fs.Write(dstBuffer, 0, (int)size);
                //}

                //Console.Write($"\r{c}");
                //sw.Start();
            }
            sw.Stop();
            var megapixels = (BigInteger)frameWidth * (BigInteger)frameHeight * (BigInteger)c;
            const ulong subHD = 1456 * 1088;
            int hd = frameWidth * frameHeight;
            double ratio = (double)hd / subHD;

            const ulong mb = 1024 * 1024;
            Console.WriteLine("Jpeg convert:");
            Console.WriteLine($"Quality      : {encoder.Quality}");
            Console.WriteLine($"Mode         : {encoder.Mode}");
            Console.WriteLine($"Elapsed      : {sw.Elapsed}");
            Console.WriteLine($"Frames       : {c}");
            Console.WriteLine($"Frame avg    : {sw.Elapsed.TotalMilliseconds / c} ms");
            Console.WriteLine($"In total     : {megapixels/ mb} MB");
            Console.WriteLine($"Out total    : {totalSize/ mb} MB");
            Console.WriteLine($"HD_F / sec   : {c / sw.Elapsed.TotalSeconds}");
            Console.WriteLine($"SubHD_F / sec: {c* ratio / sw.Elapsed.TotalSeconds}");
            Console.WriteLine($"MegaPixels /s: {megapixels / (BigInteger)sw.Elapsed.TotalSeconds / 1000000}");
            Console.WriteLine($"Jpeg MB/sec  : {totalSize / (BigInteger)sw.Elapsed.TotalSeconds / mb}");
            Console.WriteLine("=====================================");
            Console.WriteLine();
        }

        private static void ExtractFramesToJpeg(string src, string dst)
        {
            using var capture = new VideoCapture(src);
            if (!capture.IsOpened)
            {
                throw new InvalidOperationException("Failed to open video file.");
            }

            int frameWidth = (int)capture.Width;
            int frameHeight = (int)capture.Height;
            ulong bufferSize = (ulong)(frameWidth * frameHeight * 1.5);
            
            using Mat frame = new Mat();
            using Mat yuvFrame = new Mat();
            int c = 0;
            double count = capture.Get(CapProp.FrameCount);
            using JpegEncoder encoder = JpegEncoderFactory.Create(frameWidth, frameHeight, 90, bufferSize);

            Stopwatch sw = new Stopwatch();
            sw.Start();
            byte[] dstBuffer = new byte[bufferSize];
            while (true)
            {
                bool isSuccess = capture.Read(frame);
                if (!isSuccess) break;

                //Cv2.CvtColor(frame, yuvFrame, ColorConversionCodes.BGR2YUV_I420);
                
                CvInvoke.CvtColor(frame, yuvFrame, Emgu.CV.CvEnum.ColorConversion.Bgr2YuvI420);


                //SaveYUV420Frame(yuvFrame, frameWidth, frameHeight, dst);
                var size = encoder.Encode(yuvFrame.DataPointer, dstBuffer);

                FileExtensions.WriteBytes($"{c}.jpg", dstBuffer, (int)size);
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
            //var size = frame.Total() * frame.ElemSize();
            //Marshal.Copy(frame.Data, data, 0, frameSize);
            //fs.Write(data, 0, data.Length);
        }
    }
}

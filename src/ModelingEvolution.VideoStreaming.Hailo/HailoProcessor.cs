using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using ModelingEvolution.Drawing;
using ModelingEvolution.VideoStreaming.Buffers;
using ModelingEvolution.VideoStreaming.VectorGraphics;
using Rectangle = System.Drawing.Rectangle;


namespace ModelingEvolution.VideoStreaming.Hailo
{
    [StructLayout(LayoutKind.Sequential, Pack=1)]
    public struct HailoProcessorStats
    {
        [StructLayout(LayoutKind.Sequential, Pack=1)]
        public struct StageStats
        {
            public readonly ulong Processed;
            public readonly ulong Dropped;
            public readonly ulong LastIteration;
            public readonly ulong Behind;
            private readonly long _totalProcessingTimeNanoseconds;
            public readonly int ThreadCount;

            public float Fps
            {
                get
                {
                    if (TotalProcessingTime.TotalSeconds <= double.Epsilon) return 0;
                    
                    var tmp = ThreadCount / TotalProcessingTime.TotalSeconds;
                    return (float)tmp;
                }
            }
            public TimeSpan TotalProcessingTime
            {
                get
                {
                    if(Processed == 0) return TimeSpan.Zero;
                    var ns = _totalProcessingTimeNanoseconds;
                    //Console.WriteLine("Nanoseconds: " + ns);
                    //ns /= 1000; // microsecond;
                    //ns /= 1000; // miliseconds;
                    //ns /= (long)Processed;
                    //return TimeSpan.FromMilliseconds(ns,0L);
                    return TimeSpan.FromTicks(ns / 100 / (long)Processed);
                }
            }
        }

        public readonly StageStats WriteProcessing;
        public readonly StageStats ReadInterferenceProcessing;
        public readonly StageStats PostProcessing;
        public readonly StageStats CallbackProcessing;
        public readonly StageStats TotalProcessing;

        public readonly ulong InFlight;
        public readonly ulong DroppedTotal;

        public void Print(TextWriter tx = null)
        {
            tx ??= Console.Out;
            string header = "|-----------------------------------|-----------|---------|--------|---------|---------|----------------|";
            string headerRow = "| Stage                             | Processed | Dropped | Behind | Threads |   FPS   |      Time      |";

            tx.WriteLine(header);
            tx.WriteLine(headerRow);
            tx.WriteLine(header);

            PrintStageStats(tx, "Write Processing", WriteProcessing);
            PrintStageStats(tx, "Read Interference", ReadInterferenceProcessing);
            PrintStageStats(tx, "Post Processing", PostProcessing);
            PrintStageStats(tx, "Callback Processing", CallbackProcessing);
            PrintStageStats(tx, "Total Processing", TotalProcessing);

            tx.WriteLine(header);
        }

        private void PrintStageStats(TextWriter tx, string stageName, StageStats stats)
        {
            string format = "| {0,-33} | {1,9} | {2,7} | {3,6} | {4,7} | {5,7:F2} | {6,14} |";
            tx.WriteLine(format,
                stageName,
                stats.Processed,
                stats.Dropped,
                stats.Behind,
                stats.ThreadCount,
                stats.Fps,
                stats.TotalProcessingTime.WithTimeSuffix(0));
        }
    }
    
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate void NativeHandler(IntPtr results, IntPtr context);
    
    public class HailoProcessor : IDisposable
    {
        private IntPtr _nativePtr;
        private bool _disposed = false;
        private HailoProcessorStats _stats;
        [DllImport(Lib.Name, EntryPoint = "get_last_hailo_error")]
        private static extern IntPtr GetLastError();

        [DllImport(Lib.Name, EntryPoint = "hailo_processor_load_hef")]
        private static extern IntPtr LoadHef(string filename);

        [DllImport(Lib.Name, EntryPoint = "hailo_processor_stop")]
        private static extern void StopProcessor(IntPtr ptr);

        [DllImport(Lib.Name, EntryPoint = "hailo_processor_write_frame")]
        private static extern void WriteFrame(IntPtr ptr, 
            IntPtr frame, 
            uint cameraId, 
            ulong frameId, 
            int frameW, 
            int frameH,
            int roiX,
            int roiY, 
            int roiW, 
            int roiH,
            float threshold);

        [DllImport(Lib.Name, EntryPoint = "hailo_processor_start_async")]
        private static extern void StartAsyncProcessor(IntPtr ptr, IntPtr fPtr, IntPtr context);

        [DllImport(Lib.Name, EntryPoint = "hailo_processor_get_confidence")]
        private static extern float GetConfidence(IntPtr ptr);

        [DllImport(Lib.Name, EntryPoint = "hailo_processor_set_confidence")]
        private static extern void SetConfidence(IntPtr ptr, float value);

        [DllImport(Lib.Name, EntryPoint = "hailo_processor_update_stats")]
        private static extern void UpdateStats(IntPtr ptr, IntPtr stats);

        private readonly Stopwatch _sw = Stopwatch.StartNew();
        public ref HailoProcessorStats Stats
        {
            get
            {
                if (_stats.WriteProcessing.Processed != 0 && _sw.ElapsedMilliseconds <= 500) return ref _stats;
                _sw.Restart();
                ReadHailoProcessorStats();
                return ref _stats;
            }
        }

        public unsafe void ReadHailoProcessorStats()
        {
            fixed (HailoProcessorStats* ptr = &this._stats)
            {
                UpdateStats(_nativePtr, (IntPtr)ptr);
            }
        }
        private static string GetLastErrorMessage()
        {
            IntPtr errorPtr = GetLastError();
            return Marshal.PtrToStringAnsi(errorPtr);
        }
        public static HailoProcessor? Current { get; private set; }
        public static HailoProcessor Load(string fileName)
        {
            if (Current != null && Current.FileName == fileName)
                return Current;
            if (Current != null) throw new ArgumentException("Cannot load new model when Hailo is already in use.");
            
            var ptr = LoadHef(fileName);
            if (ptr == IntPtr.Zero)
                throw new HailoException(GetLastErrorMessage());
            return Current = new HailoProcessor(ptr, fileName);
        }
        public string FileName { get; }
        private HailoProcessor(IntPtr ptr, string fileName)
        {
            if (ptr == IntPtr.Zero)
                throw new ArgumentNullException("ptr cannot be zero");
            FileName = fileName;
            _nativePtr = ptr;
        }

        public void WriteFrame(IntPtr frame, 
            in FrameIdentifier id, 
            in Size frameSize, 
            in Rectangle roi, float threshold = 0.8f)
        {

            //Console.WriteLine("CSharp");
            //Console.WriteLine($"ptr: {_nativePtr:x}, " +
            //                  $"frame: {frame:x}, " +
            //                  $"cameraId: {id.CameraId}, " +
            //                  $"frameId: {id.FrameId}, " +
            //                  $"frameW: {frameSize.Width}, " +
            //                  $"frameH: {frameSize.Height}, " +
            //                  $"roiX: {roi.X}, " +
            //                  $"roiY: {roi.Y}, " +
            //                  $"roiW: {roi.Width}, " +
            //                  $"roiH: {roi.Height}");
            //Console.WriteLine();

            WriteFrame(_nativePtr, frame, id.CameraId, id.FrameId,frameSize.Width, frameSize.Height, roi.X, roi.Y, roi.Width, roi.Height, threshold);
        }

        public event EventHandler<SegmentationResult>? FrameProcessed; 
        private static void OnResult(IntPtr segmentationResult, IntPtr context)
        {
            //Console.WriteLine($"On result... segmentation result: {segmentationResult}, context: {context}");
            var sr = new SegmentationResult(segmentationResult);
            var handle = GCHandle.FromIntPtr(context);
            HailoProcessor? proc = (HailoProcessor?) handle.Target;
            if (proc == null)
            {
                Console.Error.WriteLine("Cannot find HailoProcessor Global Handle.");
                sr.Dispose();
                return;
            }
            var handler = proc.FrameProcessed;
            if (handler != null)
                handler(proc, sr);
            else sr.Dispose();
        }
        public bool IsRunning { get; private set; }
        public void StartAsync()
        {
            GCHandle contextHandle = GCHandle.Alloc(this);
            NativeHandler nhDelegate = OnResult;
            GCHandle.Alloc(nhDelegate);
            IntPtr fPtr = Marshal.GetFunctionPointerForDelegate(nhDelegate);
            StartAsyncProcessor(_nativePtr, fPtr,GCHandle.ToIntPtr(contextHandle));
            IsRunning = true;
        }

        public void Stop()
        {
            StopProcessor(_nativePtr);
        }

        public float Confidence
        {
            get => GetConfidence(_nativePtr);
            set => SetConfidence(_nativePtr, value);
        }

        public void Dispose()
        {
            if (_nativePtr != IntPtr.Zero && !_disposed)
            {
                Stop();
                _nativePtr = IntPtr.Zero;
            }
            _disposed = true;
        }
    }

    public class SegmentationResult : IDisposable, IEnumerable<Segment>
    {
        private IntPtr _nativePtr;
        private bool _disposed = false;

        [DllImport(Lib.Name, EntryPoint = "segmentation_result_get")]
        private static extern IntPtr GetSegment(IntPtr ptr, int index);

        [DllImport(Lib.Name, EntryPoint = "segmentation_result_count")]
        private static extern int GetCount(IntPtr ptr);

        [DllImport(Lib.Name, EntryPoint = "segmentation_result_dispose")]
        private static extern void DisposeSegmentationResult(IntPtr ptr);

        [DllImport(Lib.Name, EntryPoint = "segmentation_result_id")]
        private static extern FrameIdentifier SegmentationResultId(IntPtr ptr);

        [DllImport(Lib.Name, EntryPoint = "segmentation_result_roi")]
        private static extern Rectangle SegmentationResultRoi(IntPtr segment);

        [DllImport(Lib.Name, EntryPoint = "segmentation_result_threshold")]
        private static extern float SegmentationResultThreshold(IntPtr segment);

        [DllImport(Lib.Name, EntryPoint = "segmentation_result_uncertainCounter")]
        private static extern int SegmentationResultUncertainCounter(IntPtr segment);

        private ManagedArray<Segment>? _segments;

        public int Count => GetCount(_nativePtr);

        public SegmentationResult(IntPtr nativePtr)
        {
            _nativePtr = nativePtr;
        }

        public Rectangle Roi => SegmentationResultRoi(_nativePtr);
        public FrameIdentifier Id => SegmentationResultId(_nativePtr);
        public float Threshold => SegmentationResultThreshold(_nativePtr);
        public int UncertainCount => SegmentationResultUncertainCounter(_nativePtr);

        public Segment this[int index]
        {
            get
            {
                LoadSegments();
                
                return _segments[index];
            }
        }

        private void LoadSegments()
        {
            if (_segments != null) return;

            _segments = new ManagedArray<Segment>(Count);
            for (int i = 0; i < Count; i++) 
                _segments[i] = new Segment(GetSegment(_nativePtr, i));
            
        }

        public void Dispose()
        {
            if (_nativePtr != IntPtr.Zero && !_disposed)
            {
                DisposeSegmentationResult(_nativePtr);
                _nativePtr = IntPtr.Zero;
            }
            _segments?.Dispose();
            _disposed = true;
        }

        public override string ToString()
        {
            return $"Id: {Id}, Roi: {Roi}, Threshold: {Threshold}, UncertainCount: {UncertainCount}, Count: {Count}";
        }

        public IEnumerator<Segment> GetEnumerator()
        {
            LoadSegments();
            for (int i = 0; i < _segments.Count; i++)
                yield return _segments[i];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public class Segment
    {
        private readonly IntPtr _nativePtr;

        [DllImport(Lib.Name, EntryPoint = "segment_get_confidence")]
        private static extern float GetConfidence(IntPtr segment);
        [DllImport(Lib.Name, EntryPoint = "segment_get_classid")]
        private static extern int GetClassId(IntPtr segment);
        [DllImport(Lib.Name, EntryPoint = "segment_get_label")]
        private static extern IntPtr GetLabel(IntPtr segment);
        [DllImport(Lib.Name, EntryPoint = "segment_get_data")]
        private static extern IntPtr GetData(IntPtr segment);
        [DllImport(Lib.Name, EntryPoint = "segment_compute_polygon")]
        private static unsafe extern int ComputePolygon(IntPtr segment,float threshold, int* buffer, int maxSize);

        [DllImport(Lib.Name, EntryPoint = "segment_get_bbox")]
        private static extern Rectangle<float> SegmentGetBbox(IntPtr segment);



        [DllImport(Lib.Name, EntryPoint = "segment_get_resolution")]
        private static extern Size SegmentGetResolution(IntPtr segment);
        
        public Size Resolution => SegmentGetResolution(_nativePtr);

        /// <summary>
        /// Gets the bbox normalized to the resolution.
        /// </summary>
        /// <value>
        /// The bbox normalized to resolution.
        /// </value>
        public Rectangle<float> Bbox
        {
            get
            {
                var tmp = SegmentGetBbox(this._nativePtr); 
                tmp.X *= Resolution.Width;
                tmp.Y *= Resolution.Height;
                tmp.Width *= Resolution.Width;
                tmp.Height *= Resolution.Height;
                return tmp;
            }
        }

        public Segment(IntPtr nativePtr)
        {
            _nativePtr = nativePtr;
        }

        public float Confidence => GetConfidence(_nativePtr);
        public int ClassId => GetClassId(_nativePtr);
        public string Label => Marshal.PtrToStringAnsi(GetLabel(_nativePtr)) ?? string.Empty;
        

        public Mat GetMask(int width, int height)
        {
            IntPtr dataPtr = GetData(_nativePtr);
            return new Mat(height, width, DepthType.Cv32F,1, dataPtr, width);
        }

        public unsafe ManagedArray<VectorU16> ComputePolygonVectorU16(float threshold = 0.8f)
        {
            int[] buffer = ArrayPool<int>.Shared.Rent(1024 * 128);
            fixed (int* ptr = buffer)
            {
                int count = ComputePolygon(_nativePtr, threshold, ptr, buffer.Length);
                if (count == 0) return null;

                ManagedArray<VectorU16> result = new ManagedArray<VectorU16>(count / 2);
                for (int i = 0; i < count; i += 2)
                    result[i / 2] = new VectorU16((ushort)buffer[i], (ushort)buffer[i + 1]);
                
                ArrayPool<int>.Shared.Return(buffer);
                return result;
            }
        }
        public unsafe Polygon<float>? ComputePolygon(float threshold = 0.8f)
        {
            int[] buffer = ArrayPool<int>.Shared.Rent(1024 * 128);
            fixed (int* ptr = buffer)
            {
                int count = ComputePolygon(_nativePtr, threshold, ptr, buffer.Length);
                if (count == 0) return null;
                
                Polygon<float> result = new Polygon<float>(buffer.ToPointList(count));
                ArrayPool<int>.Shared.Return(buffer);
                return result;
            }
        }
    }

    static class ArrayToPointExtension
    {
        public static List<Point<float>> ToPointList(this int[] points, int size)
        {
            // Most likely should use some kind of ListPool?
            List<Point<float>> result = new List<Point<float>>(size / 2); 
            for (int i = 0; i < size; i += 2)
                result.Add(new Point<float>(points[i], points[i + 1]));
            return result;
        }
        public static IEnumerable<Point<float>> ToPoints(this int[] points, int size)
        {
            for (int i = 0; i < size; i += 2)
                yield return new Point<float>(points[i], points[i + 1]);
        }
    }
    public class HailoException : Exception
    {
        public HailoException(string message) : base(message) { }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CallbackWithContext(IntPtr segmentationResult, IntPtr context);


}
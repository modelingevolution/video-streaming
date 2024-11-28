﻿using System.Buffers;
using System.Collections;
using System.Drawing;
using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using ModelingEvolution.VideoStreaming.Buffers;


namespace ModelingEvolution.VideoStreaming.Hailo
{
    public class HailoProcessor : IDisposable
    {
        private IntPtr _nativePtr;
        private bool _disposed = false;

        [DllImport(Lib.Name, EntryPoint = "get_last_hailo_error")]
        private static extern IntPtr GetLastError();

        [DllImport(Lib.Name, EntryPoint = "hailo_processor_load_hef")]
        private static extern IntPtr LoadHef(string filename);

        [DllImport(Lib.Name, EntryPoint = "hailo_processor_stop")]
        private static extern void StopProcessor(IntPtr ptr);

        [DllImport(Lib.Name, EntryPoint = "hailo_processor_write_frame")]
        private static extern void WriteFrame(IntPtr ptr, byte[] frame, int frameW, int frameH, int roiX, int roiY, int roiW, int roiH);

        [DllImport(Lib.Name, EntryPoint = "hailo_processor_start_async")]
        private static extern void StartAsyncProcessor(IntPtr ptr, [MarshalAs(UnmanagedType.FunctionPtr)] CallbackWithContext callback, IntPtr context);

        [DllImport(Lib.Name, EntryPoint = "hailo_processor_get_confidence")]
        private static extern float GetConfidence(IntPtr ptr);

        [DllImport(Lib.Name, EntryPoint = "hailo_processor_set_confidence")]
        private static extern void SetConfidence(IntPtr ptr, float value);

        private static string GetLastErrorMessage()
        {
            IntPtr errorPtr = GetLastError();
            return Marshal.PtrToStringAnsi(errorPtr);
        }

        public static HailoProcessor Load(string fileName)
        {
            var ptr = LoadHef(fileName);
            if (ptr == IntPtr.Zero)
                throw new HailoException(GetLastErrorMessage());
            return new HailoProcessor(ptr);
        }

        private HailoProcessor(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                throw new ArgumentNullException("ptr cannot be zero");
            _nativePtr = ptr;
        }

        public void WriteFrame(byte[] frame, in Size frameSize, in Rectangle roi)
        {
            WriteFrame(_nativePtr, frame, frameSize.Width, frameSize.Height, roi.X, roi.Y, roi.Width, roi.Height);
        }

        public event EventHandler<SegmentationResult>? FrameProcessed; 
        private static void OnResult(IntPtr segmentationResult, IntPtr context)
        {
            var sr = new SegmentationResult(segmentationResult);
            var handle = GCHandle.FromIntPtr(context);
            HailoProcessor proc = (HailoProcessor) handle.Target;

            var handler = proc.FrameProcessed;
            if (handler != null)
                handler(proc, sr);
            else sr.Dispose();
        }
        public void StartAsync(CallbackWithContext callback, object context)
        {
            GCHandle contextHandle = GCHandle.Alloc(context);
            StartAsyncProcessor(_nativePtr, callback, GCHandle.ToIntPtr(contextHandle));
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

        private ManagedArray<Segment>? _segments;

        public int Count => GetCount(_nativePtr);

        public SegmentationResult(IntPtr nativePtr)
        {
            _nativePtr = nativePtr;
        }

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
            if (_segments == null) return;

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
        private static extern int ComputePolygon(IntPtr segment,float threshold, int[] buffer, int maxSize);

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

        public Point[] ComputePolygon(float threshold = 0.8f)
        {
            int[] buffer = ArrayPool<int>.Shared.Rent(1024 * 128);
            int count = ComputePolygon(_nativePtr, threshold, buffer, buffer.Length);
            if (count == 0) return Array.Empty<Point>();

            Point[] points = new Point[count];
            for (int i = 0; i < count; i++) 
                points[i] = new Point(buffer[i * 2], buffer[i * 2 + 1]);
            ArrayPool<int>.Shared.Return(buffer);
            return points;
        }
    }

    public class HailoException : Exception
    {
        public HailoException(string message) : base(message) { }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void CallbackWithContext(IntPtr segmentationResult, IntPtr context-);


}
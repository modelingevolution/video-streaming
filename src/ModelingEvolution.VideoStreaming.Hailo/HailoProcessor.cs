using System.Drawing;
using System.Runtime.InteropServices;

namespace ModelingEvolution.VideoStreaming.Hailo;

public class HailoProcessor : IDisposable
{
    private readonly IntPtr _nativePtr;
    private bool _disposed = false;
    
    [DllImport(Lib.Name, EntryPoint = "get_last_hailo_error")]
    private static extern IntPtr GetLastError();
        
    [DllImport(Lib.Name, EntryPoint = "hailo_processor_load_hef")]
    private static extern IntPtr LoadHef(string filename);
    [DllImport(Lib.Name, EntryPoint = "hailo_processor_process_frame")]
    private static extern unsafe IntPtr ProcessFrame(IntPtr ptr, byte* frame, int frameW, int frameH, int roiX, int roiY, int roiW, int roiH, int dstW, int dstH);
    [DllImport(Lib.Name, EntryPoint = "hailo_processor_dispose")]
    private static extern void DisposeProcessor(IntPtr ptr);
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

    public AnnotationResult ProcessFrame(byte[] frame, in Size frameSize, in Rectangle roi, in Size dst)
    {
        return ProcessFrame(frame, frameSize.Width, frameSize.Height, roi.X, roi.Y, roi.Width, roi.Height, dst.Width,
            dst.Height);
    }
    public unsafe AnnotationResult ProcessFrame(byte[] frame, int frameW, int frameH, int roiX, 
        int roiY, int roiW, int roiH, int dstW, int dstH)
    {
        if (frame == null) throw new ArgumentNullException("Frame cannot be null");
        
        fixed (byte *ptr = frame)
        {
            return AnnotationResult(ptr, frameW, frameH, roiX, roiY, roiW, roiH, dstW, dstH);
        }
    }

    public unsafe AnnotationResult AnnotationResult(byte* ptr, int frameW, int frameH, int roiX, int roiY, int roiW,
        int roiH, int dstW, int dstH)
    {
        IntPtr resultPtr = ProcessFrame(_nativePtr, ptr, frameW, frameH, roiX, roiY, roiW, roiH, dstW, dstH);
        if (_nativePtr == IntPtr.Zero)
            throw new HailoException(GetLastErrorMessage());

        return new AnnotationResult(resultPtr, dstW, dstH);
    }

    public float Confidence
    {
        get => GetConfidence(_nativePtr);
        set => SetConfidence(_nativePtr, value);
    }
    public void Dispose()
    {
        if(_nativePtr != IntPtr.Zero && !_disposed)
            DisposeProcessor(_nativePtr);
        _disposed = true;
    }
    
}
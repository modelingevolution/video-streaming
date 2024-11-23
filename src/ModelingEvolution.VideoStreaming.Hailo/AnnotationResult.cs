using System.Buffers;
using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace ModelingEvolution.VideoStreaming.Hailo;

public class AnnotationResult : IDisposable
{
    public readonly record struct AnnotationMask(Mat Mask, int ClassId)
    {
        
    }
    private IntPtr _nativePtr;
    private readonly int _dstW;
    private readonly int _dstH;

    private AnnotationMask[]? _items = null;
    private int _count;
    [DllImport(Lib.Name, EntryPoint = "annotation_result_get_mask")]
    private static extern IntPtr GetMask(IntPtr ptr, int index);
    [DllImport(Lib.Name, EntryPoint = "annotation_result_get_classid")]
    private static extern int GetClassId(IntPtr ptr, int index);
    [DllImport(Lib.Name, EntryPoint = "annotation_result_count")]
    private static extern int GetCount(IntPtr ptr);
    [DllImport(Lib.Name, EntryPoint = "annotation_result_dispose")]
    private static extern void DisposeAnnotationResult(IntPtr ptr);

    private void Load()
    {
        _count = GetCount(_nativePtr);
        _items = ArrayPool<AnnotationMask>.Shared.Rent(_count);
        for (int i = 0; i < _count; i++)
        {
            var maskPtr = GetMask(_nativePtr, i);
            var classId = GetClassId(_nativePtr, i);
            var mat = new Mat(_dstW, _dstH, DepthType.Cv8U,1,  maskPtr, _dstW);
            _items[i] = new AnnotationMask(mat, classId);
        }
    }

    public AnnotationMask this[int index]
    {
        get
        {
            if (_items == null) Load();
            return _items[index];
        }
    }
    public int Count
    {
        get
        {
            if (_items == null)
                Load();
            return _count;
        }
    }
    public AnnotationResult(IntPtr nativePtr, int dstW, int dstH)
    {
        _nativePtr = nativePtr;
        _dstW = dstW;
        _dstH = dstH;
    }

 

    public void Dispose()
    {
        if(_items != null)
            ArrayPool<AnnotationMask>.Shared.Return(_items);
        DisposeAnnotationResult(_nativePtr);
        _nativePtr = IntPtr.Zero;
    }
}
using System.Buffers;
using System.Drawing;
using Microsoft.ML.OnnxRuntime.Tensors;
using ModelingEvolution.VideoStreaming;
using System.Runtime.InteropServices;

namespace ModelingEvolution_VideoStreaming.Yolo
{
    public static class DenseTensorExtensions
    {
        public static unsafe void CopyInputFromYuvFrame(this DenseTensor<float> tensor, 
            YuvFrame* frame, in Rectangle interestRegion,in Size targetImgSz)
        {
            TensorPreprocessor locals = new TensorPreprocessor(frame, 
                interestRegion, 
                targetImgSz, 
                tensor);
            locals.Process();
        }
    }
}

using System.Buffers;
using System.Drawing;
using Microsoft.ML.OnnxRuntime.Tensors;
using ModelingEvolution.VideoStreaming;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ModelingEvolution_VideoStreaming.Yolo
{
    public static class DenseTensorExtensions
    {
        public static unsafe void CopyInputFromYuvFrame(this DenseTensor<float> tensor, 
            YuvFrame* frame, Rectangle* interestRegion, Size* targetImgSz)
        {
            TensorPreprocessor locals = new TensorPreprocessor(frame, 
                interestRegion, 
                targetImgSz, 
                tensor);
            locals.Process();
        }
    }

    public static class RectangleExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sigmoid(this float value) => 1 / (1 + MathF.Exp(-value));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetLuminance(this float confidence) => (byte)((confidence * 255 - 255) * -1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rectangle TransformBy(this RectangleF bbox, Size modelImgSz, Rectangle interestRegion)
        {
            var scaleX = (float)interestRegion.Width / modelImgSz.Width;
            var scaleY = (float)interestRegion.Height / modelImgSz.Height;

            var x = interestRegion.X + (int)(bbox.X * scaleX);
            var y = interestRegion.Y + (int)(bbox.Y * scaleY);
            var width = (int)(bbox.Width * scaleX);
            var height = (int)(bbox.Height * scaleY);

            return new Rectangle(x, y, width, height);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rectangle NormalizedTransformBy(this RectangleF rect, Rectangle area)
        {
            int w = area.Width;
            int h = area.Height;
            int x = area.X + (int)(w * rect.X);
            int y = area.Y + (int)(h * rect.Y);
            int tw = (int)(w * rect.Width);
            int th = (int)(h * area.Height);
            
            return new Rectangle(x, y, tw, th);
        }
        public static Rectangle NormalizedScaleBy(this RectangleF rect, Size size)
        {
            var x = (int)(rect.X * size.Width);
            var y = (int)(rect.Y * size.Height);
            var width = (int)(rect.Width * size.Width);
            var height = (int)(rect.Height * size.Height);
            return new Rectangle(x, y, width, height);
        }
    }
    public readonly unsafe ref struct TensorPreprocessor
    {
        private readonly int _strideY;
        private readonly int _strideX;
        private readonly int _strideR;
        private readonly int _strideG;
        private readonly int _strideB;

        public TensorPreprocessor(YuvFrame* frame, 
            Rectangle *interestRegion, 
            Size *targetImgSz,
            DenseTensor<float> tensor)
        {
            this.Frame = frame;
            _interestRegion = interestRegion;
            _targetImgSz = targetImgSz;
            this.Tensor = tensor;
            this._tensorSpan = Tensor.Buffer.Span;
            // Pre-calculate strides for performance
            this._strideY = this.Tensor.Strides[2];
            this._strideX = this.Tensor.Strides[3];
            this._strideR = this.Tensor.Strides[1] * 0;
            this._strideG = this.Tensor.Strides[1] * 1;
            this._strideB = this.Tensor.Strides[1] * 2;
        }

        public void Process()
        {
            if (_interestRegion->Size == *this._targetImgSz)
            {
                // no need to resize.
                int w = _targetImgSz->Width;
                int h = _targetImgSz->Height;
                for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var pixel = Frame->GetPixel(_interestRegion->X + x,_interestRegion->Y + y);
                    var tensorIndex = _strideR + _strideY * y + _strideX * x;
                    WritePixel(_tensorSpan, tensorIndex, pixel, _strideR, _strideG, _strideB);
                }
            }
            else
            {
                using var targetFrame = Frame->Resize(*_interestRegion, * _targetImgSz);

                int w = _targetImgSz->Width;
                int h = _targetImgSz->Height;
                for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var pixel = targetFrame.Frame.GetPixel(x, y);
                    var tensorIndex = _strideR + _strideY * y + _strideX * x;
                    WritePixel(_tensorSpan, tensorIndex, pixel, _strideR, _strideG, _strideB);
                }
            }
        }
       
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WritePixel(Span<float> target, int index, Color pixel, int strideBatchR, int strideBatchG, int strideBatchB)
        {
            target[index] = pixel.R / 255f;
            target[index + strideBatchG - strideBatchR] = pixel.G / 255f;
            target[index + strideBatchB - strideBatchR] = pixel.B / 255f;
        }

        public readonly YuvFrame* Frame;
        private readonly Rectangle* _interestRegion;
        private readonly Size* _targetImgSz;
        public readonly DenseTensor<float> Tensor;
        private readonly Span<float> _tensorSpan;
    }
}

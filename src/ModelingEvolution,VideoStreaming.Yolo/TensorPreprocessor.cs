using System.Drawing;
using System.Runtime.CompilerServices;
using Microsoft.ML.OnnxRuntime.Tensors;
using ModelingEvolution.VideoStreaming;

namespace ModelingEvolution_VideoStreaming.Yolo;

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
using System.Buffers;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ModelingEvolution.VideoStreaming.Yolo;

internal unsafe class SegmentationParser(YoloModelMetadata modelMetadata, 
    IRawBoundingBoxParser rawBoundingBoxParser) 
    : IParser<Segmentation>
{
    public Segmentation[] ProcessTensorToResult(YoloRawOutput output, 
        Rectangle interestRegion, float threshold)
    {
        var output0 = output.Output0;
        var output1 = output.Output1 ?? throw new Exception();

        var boxes = rawBoundingBoxParser.Parse<RawBoundingBox>(output0);
        var maskChannelCount = output0.Dimensions[1] - 4 - modelMetadata.Names.Length;

        var result = new Segmentation[boxes.Length];

        for (var index = 0; index < boxes.Length; index++)
        {
            var box = boxes[index];
            var bounds = box.Bounds.TransformBy(modelMetadata.ImageSize, interestRegion);

            using var maskWeights = CollectMaskWeights(output0, box.Index, maskChannelCount, modelMetadata.Names.Length + 4);

            var mask = ProcessMask(output1, maskWeights.Memory.Span);

            result[index] = new Segmentation
            {
                Mask = mask,
                Name = modelMetadata.Names[box.NameIndex],
                Bounds = bounds,
                Confidence = box.Confidence,
                Roi = interestRegion,
                Threshold = threshold
            };
        }

        return result;
    }

    private static Mat ProcessMask(Tensor<float> prototypes,
        ReadOnlySpan<float> weights)
    {
        var maskChannels = prototypes.Dimensions[1];
        var maskHeight = prototypes.Dimensions[2];
        var maskWidth = prototypes.Dimensions[3];

        if (maskChannels != weights.Length)
            throw new InvalidOperationException();

        var size = new Size(maskWidth, maskHeight);
        var bitmap = new Mat(size, DepthType.Cv8U, 1);
        var data = (byte*)bitmap.DataPointer;
        for (var y = 0; y < maskHeight; y++)
        {
            for (var x = 0; x < maskWidth; x++)
            {
                var value = 0F;
                for (int i = 0; i < maskChannels; i++) 
                    value += prototypes[0, i, y, x] * weights[i];
                    
                value = value.Sigmoid();
                var color = 255-value.GetLuminance();
                    
                data[y * maskWidth + x] = (byte)color;
            }
        }

        return bitmap;
    }

    private IMemoryOwner<float> CollectMaskWeights(Tensor<float> output, int boxIndex, int maskChannelCount, int maskWeightsOffset)
    {
        var weights = MemoryPool<float>.Shared.Rent(maskChannelCount);
        var weightsSpan = weights.Memory.Span;

        for (int i = 0; i < maskChannelCount; i++)
        {
            weightsSpan[i] = output[0, maskWeightsOffset + i, boxIndex];
        }

        return weights;
    }

        

      

        
}
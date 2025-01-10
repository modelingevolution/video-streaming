using System.Buffers;
using System.Drawing;
using Microsoft.ML.OnnxRuntime;
using ModelingEvolution.VideoStreaming;

namespace ModelingEvolution_VideoStreaming.Yolo;

internal class YoloModelRunner<T> : IYoloModelRunner<T> where T : IYoloPrediction<T>
{
    private readonly IParser<T> _parser;
    private readonly YoloConfiguration _configuration;
    private readonly InferenceSession _session;
    private readonly SessionTensorInfo _tensorInfo;
    private readonly RunOptions _options = new();
    public ModelPerformance Performance { get; } = new();

    public YoloModelRunner(IParser<T> parser,
        InferenceSession session,
        YoloConfiguration? configuration = null)
    {
        _parser = parser;
        _configuration = configuration ?? new YoloConfiguration();

        _session = session;
            
        _tensorInfo = new SessionTensorInfo(_session);
    }

    public unsafe YoloResult<T> Process(
        YuvFrame* frame, 
        Rectangle* interestArea, float threshold)
    {
        using var binding = PreProcess(frame, interestArea, out var output);

        ProcessInterference(binding);

        return PostProcess(frame, interestArea, threshold, output);
    }

    private unsafe YoloResult<T> PostProcess(YuvFrame* frame, Rectangle* interestArea, float threshold, YoloRawOutput output)
    {
        // Now we have output we can process the output
        using var s = Performance.MeasurePostProcessing();
            
        var result = _parser.ProcessTensorToResult(output, *interestArea, threshold);
        output.Dispose();
            
        return new YoloResult<T>(result)
        {
            ImageSize = frame->Info.Size
        };
    }

    private void ProcessInterference(OrtIoBinding binding)
    {
        using var s = Performance.MeasureInterference();
        // Do the interference
        _session.RunWithBinding(_options, binding);
    }

    private unsafe OrtIoBinding PreProcess(YuvFrame* frame, Rectangle* interestArea, out YoloRawOutput output)
    {
        using var perf = Performance.MeasurePreProcessing();
        OrtIoBinding? binding = null;
        try
        {
            binding = _session.CreateIoBinding();
            output = CreateRawOutput(binding);
            
            using var input = MemoryPool<float>.Shared.AllocateTensor<float>(_tensorInfo.Input0, true);
                                    
            var s = _configuration.ImageSize;
            var target = input.Tensor;
            target.CopyInputFromYuvFrame(frame, interestArea, &s);

            // Create ort values
            var ortInput = CreateOrtValue(target.Buffer, _tensorInfo.Input0.Dimensions64);
            
            // Bind input to ort io binding
            binding.BindInput(_session.InputNames[0], ortInput);
            return binding;
        }
        catch
        {
            binding?.Dispose();
            throw;
        }
    }

    private YoloRawOutput CreateRawOutput(OrtIoBinding binding)
    {
        var output0Info = _tensorInfo.Output0;
        var output1Info = _tensorInfo.Output1;

        // Allocate output0 tensor buffer
        var output0 = MemoryPool<float>.Shared.AllocateTensor(output0Info);

        // Bind tensor buffer to ort binding
        binding.BindOutput(_session.OutputNames[0], CreateOrtValue(output0.Tensor.Buffer, output0Info.Dimensions64));

        if (output1Info != null)
        {
            // Allocate output1 tensor buffer
            var output1 = MemoryPool<float>.Shared.AllocateTensor(output1Info.Value);

            // Bind tensor buffer to ort binding
            binding.BindOutput(_session.OutputNames[1], CreateOrtValue(output1.Tensor.Buffer, output1Info.Value.Dimensions64));

            return new YoloRawOutput(output0, output1);
        }

        return new YoloRawOutput(output0, null);
    }

    private static OrtValue CreateOrtValue(Memory<float> buffer, long[] shape)
    {
        return OrtValue.CreateTensorValueFromMemory(OrtMemoryInfo.DefaultInstance, buffer, shape);
    }
}
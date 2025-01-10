using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;

namespace ModelingEvolution.VideoStreaming.Yolo;

public class ModelFactory(ILoggerFactory loggerFactory)
{
    public IAsyncSegmentationModelRunner<ISegmentation> LoadSegmentationModel(string modelFullPath)
    {
        var extension = Path.GetExtension(modelFullPath);
        if(extension == ".onnx")
            return LoadOnnxAsyncSegmentationModel(modelFullPath);
        else if (extension == ".hef")
            return LoadHefSegmentationModel(modelFullPath);
        throw new NotSupportedException($"{extension} is not supported.");
    }

    public IAsyncSegmentationModelRunner<ISegmentation> LoadHefSegmentationModel(string modelFullPath)
    {
        if (File.Exists(modelFullPath))
            return new HailoModelRunner(modelFullPath, loggerFactory.CreateLogger<HailoModelRunner>());
        throw new FileNotFoundException("Model file was not found.", modelFullPath);
    }
    public IAsyncSegmentationModelRunner<ISegmentation> LoadOnnxAsyncSegmentationModel(string segYoloModelFile)
    {
        var session = PrepareOnnx(segYoloModelFile, out var onnxConfiguration, out var segParser);
        return new YoloOnnxModelRunner(segParser, session, onnxConfiguration);
    }

    private InferenceSession PrepareOnnx(string segYoloModelFile, out YoloOnnxConfiguration onnxConfiguration,
        out SegmentationParser segParser)
    {
        var options = new YoloPredictorOptions();
        var model = File.ReadAllBytes(segYoloModelFile);
        var session = options.CreateSession(model);
        var metadata = new YoloMetadata(session);
        onnxConfiguration = new YoloOnnxConfiguration();
        var bbParser = new RawBoundingBoxParser(metadata, onnxConfiguration, new NonMaxSuppressionService());
        segParser = new SegmentationParser(metadata, bbParser);
        return session;
    }

    public ISegmentationModelRunner<ISegmentation> LoadOnnxSegmentationModel(string segYoloModelFile)
    {
        var session = PrepareOnnx(segYoloModelFile, out var onnxConfiguration, out var segParser);
        return new YoloOnnxModelRunner(segParser, session, onnxConfiguration);
    }
}
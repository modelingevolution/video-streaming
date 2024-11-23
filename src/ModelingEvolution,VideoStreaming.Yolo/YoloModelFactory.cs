namespace ModelingEvolution_VideoStreaming.Yolo;

public static class ModelFactory
{
    public static ISegmentationModelRunner<ISegmentation> LoadSegmentationModel(string modelFullPath)
    {
        var extension = Path.GetExtension(modelFullPath);
        if(extension == ".onnx")
            return LoadOnnxSegmentationModel(modelFullPath);
        else if (extension == ".hef")
            return LoadHefSegmentationModel(modelFullPath);
        throw new NotSupportedException($"{extension} is not supported.");
    }

    private static ISegmentationModelRunner<ISegmentation> LoadHefSegmentationModel(string modelFullPath)
    {
        if (File.Exists(modelFullPath))
            return new HailoModelRunner(modelFullPath);
        throw new FileNotFoundException("Model file was not found.", modelFullPath);
    }

    private static ISegmentationModelRunner<ISegmentation> LoadOnnxSegmentationModel(string segYoloModelFile)
    {
        var options = new YoloPredictorOptions();
        var model = File.ReadAllBytes(segYoloModelFile);
        var session = options.CreateSession(model);
        var metadata = new YoloMetadata(session);
        YoloOnnxConfiguration onnxConfiguration = new YoloOnnxConfiguration();
        var bbParser = new RawBoundingBoxParser(metadata, onnxConfiguration, new NonMaxSuppressionService());
        var segParser = new SegmentationParser(metadata, bbParser);
        return new YoloOnnxModelRunner(segParser, session, onnxConfiguration);
    }
}
namespace ModelingEvolution_VideoStreaming.Yolo;

public static class YoloModelFactory
{
    public static IYoloModelRunner<Segmentation> LoadSegmentationModel(string segYoloModelFile)
    {
        var options = new YoloPredictorOptions();

        var model = File.ReadAllBytes(segYoloModelFile);
        var session = options.CreateSession(model);
        var metadata = new YoloMetadata(session);
        YoloConfiguration configuration = new YoloConfiguration();
        var bbParser = new RawBoundingBoxParser(metadata, configuration, new NonMaxSuppressionService());
        var segParser = new SegmentationParser(metadata, bbParser);
        return new YoloModelRunner<Segmentation>(segParser, session, configuration);
    }
}
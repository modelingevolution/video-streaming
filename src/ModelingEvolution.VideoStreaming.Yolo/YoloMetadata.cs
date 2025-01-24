﻿using System.Drawing;
using Microsoft.ML.OnnxRuntime;

namespace ModelingEvolution.VideoStreaming.Yolo;

public class YoloModelMetadata
{
    public string Author { get; }

    public string Description { get; }

    public string Version { get; }

    public int BatchSize { get; }

    public Size ImageSize { get; }

    public YoloTask Task { get; }

    public SegmentationClass[] Names { get; }

    public YoloArchitecture Architecture { get; }

    internal YoloModelMetadata(InferenceSession session)
    {
        var metadata = session.ModelMetadata.CustomMetadataMap;

        Author = metadata["author"];
        Description = metadata["description"];
        Version = metadata["version"];

        Task = metadata["task"] switch
        {
            "obb" => YoloTask.Obb,
            "pose" => YoloTask.Pose,
            "detect" => YoloTask.Detect,
            "segment" => YoloTask.Segment,
            "classify" => YoloTask.Classify,
            _ => throw new InvalidOperationException("Unknow YoloV8 'task' value")
        };

        if (Task == YoloTask.Detect && session.OutputMetadata.Values.First().Dimensions[2] == 6) // YOLOv10 output shape => [<batch>, 300, 6]
        {
            Architecture = YoloArchitecture.YoloV10;
        }

        BatchSize = int.Parse(metadata["batch"]);
        ImageSize = ParseSize(metadata["imgsz"]);
        Names = ParseNames(metadata["names"]);
    }

    public static YoloModelMetadata Parse(InferenceSession session)
    {
        try
        {
            if (session.ModelMetadata.CustomMetadataMap["task"] == "pose")
            {
                return new YoloModelPoseMetadata(session);
            }

            return new YoloModelMetadata(session);
        }
        catch (Exception inner)
        {
            throw new InvalidOperationException("The metadata parsing failed, making sure you use an official YOLOv8 model", inner);
        }
    }

    #region Parsers

    private static Size ParseSize(string text)
    {
        text = text[1..^1]; // '[640, 640]' => '640, 640'

        var split = text.Split(", ");

        var y = int.Parse(split[0]);
        var x = int.Parse(split[1]);

        return new Size(x, y);
    }

    private static SegmentationClass[] ParseNames(string text)
    {
        text = text[1..^1];

        var split = text.Split(", ");
        var count = split.Length;

        var names = new SegmentationClass[count];

        for (int i = 0; i < count; i++)
        {
            var value = split[i];

            var valueSplit = value.Split(": ");

            var id = int.Parse(valueSplit[0]);
            var name = valueSplit[1][1..^1].Replace('_', ' ');

            names[i] = new SegmentationClass(id, name);
        }

        return names;
    }

    #endregion
}
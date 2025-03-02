﻿namespace ModelingEvolution.VideoStreaming.Yolo;

public interface IYoloPrediction<in TSelf> : IDisposable
{
    internal static abstract string Describe(TSelf[] predictions);
}
﻿using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming.Recordings;

[OutputStream("Dataset")]
public class DatasetRecordingStarted
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Failed { get; set; }
    public string Error { get; set; }

}
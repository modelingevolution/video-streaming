﻿using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming.Recordings;

[OutputStream("Dataset")]
public class StopDatasetRecording
{
    public Guid Id { get; set; } = Guid.NewGuid();

}
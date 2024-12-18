﻿using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming.Recordings;

[OutputStream("Datasets")]
public class StartDatasetRecording 
{
    public Guid Id { get; set; } = Guid.NewGuid();

}
[OutputStream("Datasets")]
public class FindMissingDatasetRecordings
{
    public Guid Id { get; set; } = Guid.NewGuid();

}
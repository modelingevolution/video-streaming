using System.Text.Json.Serialization;

namespace ModelingEvolution.VideoStreaming.CVat;

public class CvatTaskResponse
{
    public string Url { get; set; }
    public int Id { get; set; }
    public string Name { get; set; }
    public int ProjectId { get; set; }
    public string Mode { get; set; }
    public User Owner { get; set; }
    public User Assignee { get; set; }
    public string BugTracker { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
    public int? Overlap { get; set; }
    public int SegmentSize { get; set; }
    public string Status { get; set; }
    public int DataChunkSize { get; set; }
    public string DataCompressedChunkType { get; set; }
    public int GuideId { get; set; }
    public string DataOriginalChunkType { get; set; }
    public int Size { get; set; }
    
    [JsonPropertyName("image_quality")]
    public int ImageQuality { get; set; }
    public int Data { get; set; }
    public string Dimension { get; set; }
    public string Subset { get; set; }
    public string Organization { get; set; }
    public Storage TargetStorage { get; set; }
    public Storage SourceStorage { get; set; }
    public Job Jobs { get; set; }
    public Label Labels { get; set; }
    public DateTime AssigneeUpdatedDate { get; set; }
}
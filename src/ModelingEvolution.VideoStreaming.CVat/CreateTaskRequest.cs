using System.Text.Json.Serialization;

namespace ModelingEvolution.VideoStreaming.CVat;

public class CreateTaskRequest
{
    public string Name { get; set; }
    [JsonPropertyName("project_id")]
    public int? ProjectId { get; set; }
    public int? OwnerId { get; set; }
    public int? AssigneeId { get; set; }
    public string BugTracker { get; set; }
    public int? Overlap { get; set; }
    public int SegmentSize { get; set; }
    public List<LabelRequest> Labels { get; set; }
    public string Subset { get; set; }
    public Storage TargetStorage { get; set; }
    public Storage SourceStorage { get; set; }
}
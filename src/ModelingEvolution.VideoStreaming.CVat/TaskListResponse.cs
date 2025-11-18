namespace ModelingEvolution.VideoStreaming.CVat;

public class TaskListResponse
{
    public int Count { get; set; }
    public string Next { get; set; }
    public string Previous { get; set; }
    public List<CvatTaskResponse> Results { get; set; }
}
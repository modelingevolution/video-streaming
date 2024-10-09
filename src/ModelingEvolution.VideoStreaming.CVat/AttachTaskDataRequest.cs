using System.Text.Json.Serialization;

namespace ModelingEvolution.VideoStreaming.CVat;

public class AttachTaskDataRequest
{
    
    [JsonPropertyName("image_quality")]
    public int ImageQuality { get; set; }
    
    [JsonPropertyName("remote_files")]
    public List<string> RemoteFiles { get; set; } = new List<string>();
    
    [JsonPropertyName("copy_data")]
    public bool CopyData { get; set; } = false;
    
    [JsonPropertyName("storage")]
    public string Storage { get; set; } 
}
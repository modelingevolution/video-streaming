using System.Text.Json.Serialization;
using MicroPlumberd;

namespace ModelingEvolution.VideoStreaming;

[OutputStream("StreamServerConfig")]
public class ServerConfig
{
    public List<Uri> Sources { get; set; }

    [JsonIgnore]
    public Guid Id { get; set; } 
    [JsonIgnore]
    public long Version { get; set; } = -1;

    public ServerConfig()
    {
        Sources = new List<Uri>();
    }
}
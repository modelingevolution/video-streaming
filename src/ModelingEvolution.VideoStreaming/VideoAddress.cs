using System;
using System.Text;

namespace ModelingEvolution.VideoStreaming;

public enum Transport
{
    Tcp,Udp,Shm
}

public enum VideoProtocol
{
    Mjpeg,H264
}
public readonly struct VideoAddress
{
    public static VideoAddress CreateFrom(Uri uri)
    {
        var proto = uri.Scheme;
        var host = uri.Host;
        var port = uri.Port;
        var path = uri.PathAndQuery.TrimStart('/').Split(',');
        var streamName = path.Any() ? path[0] : string.Empty;

        string queryString = uri.Query;
        var queryParameters = System.Web.HttpUtility.ParseQueryString(queryString);
        string? tagsString = queryParameters["tags"];

        var tags = Array.Empty<string>();
        if (!string.IsNullOrEmpty(tagsString)) 
            tags=tagsString.Split(',');
        
        return new VideoAddress(Enum.Parse<VideoProtocol>(proto), host, port, streamName, tags);
    }
    private readonly string _str;

    public string FriendlyName
    {
        get
        {
            if (String.IsNullOrWhiteSpace(StreamName))
                return Host;
            else 
                return $"{Host}/{StreamName}";
        }
    }
    public string? StreamName { get; init; }
    public string Host { get; init; }
    public int Port { get; init; }
    public Transport Transport => Port == 0 ? Transport.Shm : Transport.Tcp;
    public VideoProtocol Protocol { get; init; }
    public HashSet<string>? Tags { get; init; }
    public Uri Uri => new Uri(_str);

    public VideoAddress(VideoProtocol protocol, string host="localhost", 
        int port=0, string? streamName = null, params string[] tags)
    {
        Host = host;
        Port = port;
        StreamName = streamName;
        Protocol = protocol;
        Tags = tags.ToHashSet();
        StringBuilder sb = new(protocol.ToString());
        sb.Append($"://{Host}");
        if(port > 0)
            sb.Append($":{Port}");
        if(!string.IsNullOrWhiteSpace(StreamName))
            sb.Append($"/{StreamName}");
        if (tags.Any())
        {
            string tagsParam = string.Join(',', tags);
            sb.Append($"?tags={tagsParam}");
        }
        _str = sb.ToString();
    }

    public override string ToString() => _str;
}
using System;
using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ModelingEvolution.VideoStreaming;

public enum VideoSourceApi
{
    Libcamera,
    OpenVidCam
}
public static class EnumFlagsUtility
{
    // Check if an enum flag is set
    public static bool HasFlag<T>(this T value, T flag) where T : Enum
    {
        var intValue = Convert.ToInt32(value);
        var flagValue = Convert.ToInt32(flag);
        return (intValue & flagValue) == flagValue;
    }

    // Add a flag to an enum value
    public static T AddFlag<T>(this T value, T flag) where T : Enum
    {
        var intValue = Convert.ToInt32(value);
        var flagValue = Convert.ToInt32(flag);
        return (T)Enum.ToObject(typeof(T), intValue | flagValue);
    }

    // Remove a flag from an enum value
    public static T RemoveFlag<T>(this T value, T flag) where T : Enum
    {
        var intValue = Convert.ToInt32(value);
        var flagValue = Convert.ToInt32(flag);
        return (T)Enum.ToObject(typeof(T), intValue & ~flagValue);
    }

    // Toggle a flag (add if not present, remove if present)
    public static T ToggleFlag<T>(this T value, T flag) where T : Enum
    {
        var intValue = Convert.ToInt32(value);
        var flagValue = Convert.ToInt32(flag);
        return (T)Enum.ToObject(typeof(T), intValue ^ flagValue);
    }

    public static string FlagToString<T>(this T value) where T : Enum
    {
        // returns names of the flags that are set concatenated with '+'
        return string.Join("+", Enum.GetValues(typeof(T)).Cast<T>().Where(x => value.HasFlag(x)).Select(f => f.ToString()));
    }
}

public readonly struct VideoAddress : IParsable<VideoAddress>
{
    
    static bool RecognizeProtocol<T>(IList<string> list, out T e) where T : Enum
    {
        bool isSet = false;
        e = default(T)!;
        for (int i = 0; i < list.Count; i++)
        {
            if (!Enum.TryParse(typeof(T), list[i], true, out var result)) continue;
            list.RemoveAt(i--);
            if (isSet)
                e = e.AddFlag((T)result);
            else
            {
                e = (T)result;
                isSet = true;
            }
        }

        return isSet;
    }
    
    public static VideoAddress CreateFrom(Uri uri)
    {
        var proto = uri.Scheme.Split('+', StringSplitOptions.RemoveEmptyEntries).ToList();

        var host = uri.Host;
        var port = uri.Port == -1 ? 0 : uri.Port;
        var path = uri.AbsolutePath.TrimStart('/').Split(',');
        var streamName = path.Any() ? path[0] : string.Empty;

        string queryString = uri.Query;
        var queryParameters = System.Web.HttpUtility.ParseQueryString(queryString);
        string? tagsString = queryParameters["tags"];

        var tags = Array.Empty<string>();
        if (!string.IsNullOrEmpty(tagsString))
            tags = tagsString.Split(',');

        string? resolution = queryParameters["resolution"];
        string? file = queryParameters["file"];
        string? cameraNr = queryParameters["camera"];
        string? videoSourceApi = queryParameters["video-api"];

        VideoSourceApi? vsapi = null;
        if (!string.IsNullOrWhiteSpace(videoSourceApi))
            vsapi = (VideoSourceApi)Enum.Parse(typeof(VideoSourceApi), videoSourceApi, true);
        
        VideoResolution rv = VideoResolution.FullHd;
        if (!string.IsNullOrEmpty(resolution))
            rv = Enum.Parse<VideoResolution>(resolution, true);
        RecognizeProtocol<VideoCodec>(proto, out var codec);
        RecognizeProtocol<VideoTransport>(proto, out var transport);
        int? camera = null;
        if (cameraNr != null && int.TryParse(cameraNr, out var c))
            camera = c;

        var vs = file != null ? VideoSource.File : (host == "localhost" ? VideoSource.Camera : VideoSource.Stream);
        if (vs == VideoSource.Camera && !vsapi.HasValue)
            vsapi = VideoSourceApi.Libcamera;

        if (!string.IsNullOrWhiteSpace(file) && vs != VideoSource.File) 
            Debug.WriteLine("Video source is not set to 'file', but file-name was specified. File will be ignored.");
        
        return new VideoAddress(codec, host, port, streamName, rv, transport,vs ,file, camera, vsapi, tags);
    }

    private readonly string _str;
    public VideoResolution Resolution { get; }
    public VideoSourceApi? SourceApi { get; }
    public string FriendlyName
    {
        get
        {
            if (String.IsNullOrWhiteSpace(StreamName))
                return $"{Host} [{VideoTransport.ToString().ToLower()}+{Codec.ToString().ToLower()} from {VideoSource.ToString().ToLower()}]";
            else if(VideoSource == VideoSource.Camera)
                return $"{Host}/{StreamName} [{VideoTransport.ToString().ToLower()}+{Codec.ToString().ToLower()} from {VideoSource.ToString().ToLower()}]";
            else return $"{StreamName} [{VideoTransport.ToString().ToLower()}+{Codec.ToString().ToLower()} from {VideoSource.ToString().ToLower()}]";
        }
    }

    public string? StreamName { get; init; }
    public string Host { get; init; }
    public int Port { get; init; }
    public int? CameraNumber { get; init; }
    public VideoTransport VideoTransport { get; init; } = VideoTransport.Tcp;
    public VideoCodec Codec { get; init; }
    public HashSet<string>? Tags { get; init; }
    public Uri Uri => new Uri(_str);
    public string? File { get; init; }
    public VideoSource VideoSource { get; init; }
    
    public VideoAddress(VideoCodec codec, string host = "localhost",
        int port = 0, string? streamName = null,
        VideoResolution resolution = VideoResolution.FullHd,
        VideoTransport vt = VideoTransport.Tcp,
        VideoSource vs = VideoSource.Camera,
        string? file = null,
        int? cameraNr = null,
        VideoSourceApi? vsapi = null,
        params string[] tags)
    {
        if (string.IsNullOrWhiteSpace(file) && vs == VideoSource.File)
            throw new ArgumentException("File has to set when video-source is set to file.");

        SourceApi = vsapi;
        Resolution = resolution;
        Host = host;
        Port = port;
        StreamName = streamName;
        Codec = codec;
        Tags = tags.ToHashSet();
        VideoTransport = vt;
        File = file;
        CameraNumber = cameraNr;
        VideoSource = vs;
        StringBuilder sb = new($"{VideoTransport.FlagToString()}+{Codec}".ToLower());
        sb.Append($"://{Host}");
        if (port > 0)
            sb.Append($":{Port}");
        if (!string.IsNullOrWhiteSpace(StreamName))
            sb.Append($"/{StreamName}");
        var query = false;
        if (tags.Any())
        {
            string tagsParam = string.Join(',', tags);
            sb.Append($"?tags={tagsParam}");
            query = true;
        }

        if (resolution != VideoResolution.FullHd)
        {
            sb.Append(!query ? "?" : "&");
            sb.Append($"resolution={resolution}");
            query = true;
        }
        if (!string.IsNullOrWhiteSpace(File))
        {
            sb.Append(!query ? "?" : "&");
            sb.Append($"file={UrlEncoder.Default.Encode(File)}");
            query = true;
        }
        
        if(cameraNr.HasValue && cameraNr > 0)
        {
            sb.Append(!query ? "?" : "&");
            sb.Append($"camera={cameraNr.Value}");
            query = true;
        }

        if (vsapi.HasValue)
        {
            sb.Append(!query ? "?" : "&");
            sb.Append($"video-api={vsapi.Value}");
            query = true;
        }
        _str = sb.ToString();
    }

    public override string ToString() => _str;
    public static VideoAddress Parse(string s, IFormatProvider? provider = null)
    {
        return VideoAddress.CreateFrom(new System.Uri(s));
    }

    public static bool TryParse(string? s, IFormatProvider? provider, out VideoAddress result)
    {
        result = default;
        if (!System.Uri.TryCreate(s, UriKind.RelativeOrAbsolute, out var u)) return false;
        try
        {
            result = VideoAddress.CreateFrom(u);
            return true;
        }
        catch { }
        return false;
    }
    public static bool operator ==(VideoAddress a, VideoAddress b) => a.Equals(b);
    public static bool operator !=(VideoAddress a, VideoAddress b) => !a.Equals(b);
    public override bool Equals(object? obj)
    {
        return obj is VideoAddress address &&
               Resolution == address.Resolution &&
               StreamName == address.StreamName &&
               Host == address.Host &&
               Port == address.Port &&
               VideoTransport == address.VideoTransport &&
               Codec == address.Codec &&
               SourceApi == address.SourceApi &&
               !Tags.Except(address.Tags).Any() &&
               File == address.File &&
               CameraNumber == address.CameraNumber &&
               VideoSource == address.VideoSource;
    }
}
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using EventPi.Abstractions;

namespace ModelingEvolution.VideoStreaming;

/// <summary>
/// In the form of {HostName}/cam-{CameraNumber}/{data} or {HostName}/file-{FileName}/{data}
/// </summary>
/// <seealso cref="System.IParsable&lt;ModelingEvolution.VideoStreaming.VideoIdentifier&gt;" />
[JsonConverter(typeof(JsonParsableConverter<VideoRecordingIdentifier>))]
public readonly struct VideoRecordingIdentifier : IParsable<VideoRecordingIdentifier>
{
    public required HostName HostName { get; init; }
    public int? CameraNumber { get; init; }
    public string FileName { get; init; }
    public required DateTime CreatedTime { get; init; }
    public static VideoRecordingIdentifier Parse(string s, IFormatProvider? provider)
    {
        if (string.IsNullOrEmpty(s)) throw new ArgumentNullException(nameof(s));

        var parts = s.Split('/');
        
        if (parts.Length < 2) throw new FormatException("Invalid format for VideoSourceIdentifier.");

        var hostName = HostName.Parse(parts[0], provider);
        
        var createdTime = parts.Length == 3 ? DateTime.ParseExact(parts[^1], "yyyyMMdd_HHmmss", provider) : DateTime.MinValue;

        if (parts[1].StartsWith("cam-"))
        {
            var cameraNumber = int.Parse(parts[1].Substring(4));
            return new VideoRecordingIdentifier
            {
                HostName = hostName, CameraNumber = cameraNumber, CreatedTime = createdTime
            };
        }
        else if (parts[1].StartsWith("file-"))
        {
            var fileName = parts[1].Substring(5);
            return new VideoRecordingIdentifier { HostName = hostName, FileName = fileName, CreatedTime = createdTime };
        }
        else
        {
            throw new FormatException("Invalid format for VideoSourceIdentifier.");
        }
    }
    public static implicit operator Guid(VideoRecordingIdentifier addr)
    {
        return addr.ToString().ToGuid();
    }

    public static implicit operator VideoRecordingDevice(VideoRecordingIdentifier addr)
    {
        return new VideoRecordingDevice { HostName = addr.HostName, CameraNumber = addr.CameraNumber, FileName = addr.FileName};
    }
    public static implicit operator VideoRecordingIdentifier(VideoAddress addr)
    {
        return addr.VideoSource == VideoSource.File
            ? new VideoRecordingIdentifier
            {
                HostName = HostName.Parse(addr.Host), FileName = Path.GetFileName(addr.File), 
                CreatedTime = DateTime.Now
            }
            : new VideoRecordingIdentifier { HostName = HostName.Parse(addr.Host), CameraNumber = addr.CameraNumber, CreatedTime = DateTime.Now };
    }
    public static implicit operator VideoRecordingIdentifier(CameraAddress addr)
    {
        return new VideoRecordingIdentifier { HostName = addr.HostName, CameraNumber = addr.CameraNumber, FileName = null, CreatedTime = DateTime.Now };
    }
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out VideoRecordingIdentifier result)
    {
        result = default;

        if (string.IsNullOrEmpty(s)) return false;

        var parts = s.Split('/');
        if (parts.Length < 2) return false;

        if (!HostName.TryParse(parts[0], provider, out var hostName)) return false;

        
        if (parts.Length == 3 && !DateTime.TryParseExact(parts[^1], "yyyyMMdd_HHmmss", provider, System.Globalization.DateTimeStyles.None,
                out var createdTime))
            return false;
        
        else createdTime = DateTime.MinValue;
        
        if (parts[1].StartsWith("cam-"))
        {
            if (int.TryParse(parts[1].Substring(4), out var cameraNumber))
            {
                result = new VideoRecordingIdentifier
                {
                    HostName = hostName, CameraNumber = cameraNumber, CreatedTime = createdTime
                };
                return true;
            }
        }
        else if (parts[1].StartsWith("file-"))
        {
            var fileName = parts[1].Substring(5);
            result = new VideoRecordingIdentifier { HostName = hostName, FileName = fileName, CreatedTime = createdTime };
            return true;
        }

        return false;
    }

    public override string ToString()
    {
        var ct = CreatedTime.ToString("yyyyMMdd_HHmmss");
        
        return !string.IsNullOrEmpty(FileName) ? $"{HostName}/file-{FileName}/{ct}" 
            : $"{HostName}/cam-{CameraNumber ?? 0}/{ct}";
    }
}
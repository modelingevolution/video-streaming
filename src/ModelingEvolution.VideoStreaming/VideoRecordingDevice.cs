using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using EventPi.Abstractions;

namespace ModelingEvolution.VideoStreaming;

/// <summary>
/// In the form of {HostName}/cam-{CameraNumber} or {HostName}/file-{FileName}
/// </summary>
/// <seealso cref="System.IParsable&lt;ModelingEvolution.VideoStreaming.VideoIdentifier&gt;" />
[JsonConverter(typeof(JsonParsableConverter<VideoRecordingIdentifier>))]
public readonly struct VideoRecordingDevice : IParsable<VideoRecordingDevice>
{
    public required HostName HostName { get; init; }
    public int? CameraNumber { get; init; }
    public string FileName { get; init; }

    public override string ToString()
    {
        if (CameraNumber.HasValue)
            return $"{HostName}/cam-{CameraNumber.Value}";
        else if (!string.IsNullOrEmpty(FileName))
            return $"{HostName}/file-{FileName}";
        else
            throw new InvalidOperationException("VideoRecordingDevice must have either a CameraNumber or a FileName.");
    }
    public static implicit operator VideoRecordingDevice(VideoAddress addr)
    {
        return addr.VideoSource == VideoSource.File
            ? new VideoRecordingDevice
            {
                HostName = HostName.Parse(addr.Host),
                FileName = Path.GetFileName(addr.File)
            }
            : new VideoRecordingDevice { HostName = HostName.Parse(addr.Host), CameraNumber = addr.CameraNumber ?? 0};
    }
    public static implicit operator VideoRecordingDevice(CameraAddress addr)
    {
        return new VideoRecordingDevice { HostName = addr.HostName, CameraNumber = addr.CameraNumber, FileName = null };
    }
    public static VideoRecordingDevice Parse(string s, IFormatProvider? provider)
    {
        Console.WriteLine($"==> Parsing VideoRecordingDevice: {s}");
        if (string.IsNullOrEmpty(s)) 
            throw new ArgumentNullException(nameof(s));

        var parts = s.Split('/');
        if (parts.Length != 2)
        {
            throw new FormatException(
                "Invalid format. Expected format: {HostName}/cam-{CameraNumber} or {HostName}/file-{FileName}");
            
        }

        var hostName = HostName.Parse(parts[0], provider);
        var secondPart = parts[1];

        if (secondPart.StartsWith("cam-"))
        {
            if (int.TryParse(secondPart.Substring(4), out var cameraNumber))
                return new VideoRecordingDevice { HostName = hostName, CameraNumber = cameraNumber };
            else
                throw new FormatException("Invalid camera number format.");
        }
        else if (secondPart.StartsWith("file-"))
        {
            var fileName = secondPart.Substring(5);
            if (!string.IsNullOrEmpty(fileName))
                return new VideoRecordingDevice { HostName = hostName, FileName = fileName };
            else
                throw new FormatException("Invalid file name format.");
        }
        else
        {
            throw new FormatException(
                "Invalid format. Expected format: {HostName}/cam-{CameraNumber} or {HostName}/file-{FileName}");
        }
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out VideoRecordingDevice result)
    {
        result = default;
        if (string.IsNullOrEmpty(s)) return false;

        var parts = s.Split('/');
        if (parts.Length != 2) return false;

        if (!HostName.TryParse(parts[0], provider, out var hostName)) return false;

        var secondPart = parts[1];
        if (secondPart.StartsWith("cam-"))
        {
            if (int.TryParse(secondPart.Substring(4), out var cameraNumber))
            {
                result = new VideoRecordingDevice { HostName = hostName, CameraNumber = cameraNumber };
                return true;
            }
        }
        else if (secondPart.StartsWith("file-"))
        {
            var fileName = secondPart.Substring(5);
            if (!string.IsNullOrEmpty(fileName))
            {
                result = new VideoRecordingDevice { HostName = hostName, FileName = fileName };
                return true;
            }
        }

        return false;
    }
}
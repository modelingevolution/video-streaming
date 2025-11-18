using Emgu.CV;
using System.Text;
using System.Text.RegularExpressions;

namespace ModelingEvolution.VideoStreaming;

#pragma warning disable CS4014
public record Recording(string FileName, string FullPath, string Name, DateTime Started, TimeSpan Duration, Bytes Size)
{
    const string mp4_pattern = @"^(.+?)\.(\d{4})(\d{2})(\d{2})\.((?:\d{1,2}\.)?)(\d{2})(\d{2})(\d{2})-(\d+)\.mp4$";
    const string mjpeg_pattern = @"^(.+?)\.(\d{4})(\d{2})(\d{2})\.((?:\d{1,2}\.)?)(\d{2})(\d{2})(\d{2})-(\d+)\.mjpeg";
    static readonly Regex mp4_regex = new Regex(mp4_pattern, RegexOptions.Compiled);
    static readonly Regex mjpeg_regex = new Regex(mjpeg_pattern, RegexOptions.Compiled);
    public static Recording? Get(string fullName)
    {
        if (fullName.EndsWith(".mp4"))
            return Get(fullName, mp4_regex);
        else if (fullName.Equals(".mjpeg"))
            return Get(fullName, mjpeg_regex);
        return null;
    }
    private static TimeSpan GetDuration(string file)
    {
        using (var capture = new VideoCapture(file))
        {
            if (!capture.IsOpened)
            {
                Console.WriteLine("Unable to open the video file.");
                return TimeSpan.Zero;
            }

            double fps = capture.Get(Emgu.CV.CvEnum.CapProp.Fps); // Frames per second
            int frameCount = (int)capture.Get(Emgu.CV.CvEnum.CapProp.FrameCount); // Total number of frames

            double durationSeconds = frameCount / fps;
            return TimeSpan.FromSeconds(durationSeconds);

        }
    }
    public static Recording MakeRecording(string file)
    {
        var parsed = Get(file);
        if (parsed != null) return parsed;

        DateTime n = new FileInfo(file).LastWriteTime;
        var duration = GetDuration(file);
        string fn = Path.GetFileNameWithoutExtension(file);
        string folder = Path.GetDirectoryName(file);
        string extension = Path.GetExtension(file);

        StringBuilder outFile = new StringBuilder(fn.Replace(".", "_"));

        var d = n.ToString("yyyyMMdd");
        var t = n.TimeOfDay;

        outFile.Append($".{d}.{t.Hours:D2}{t.Minutes:D2}{t.Seconds:D2}-{(int)duration.TotalSeconds}{extension}");

        var result = Path.Combine(folder, outFile.ToString());

        File.Move(file, result);

        return Get(result)!;

    }
    public static IEnumerable<string> GetMissing(string dataDir)
    {
        var files = Directory.Exists(dataDir) ? Directory.EnumerateFiles(dataDir, "*.mp4")
        .Union(Directory.EnumerateFiles(dataDir, "*.mjpeg")) : Array.Empty<string>();

        foreach (var file in files)
        {
            var r = Get(file);
            if (r == null) yield return file;
        }
    }
    public static IEnumerable<Recording> Load(string dataDir)
    {
        return Directory.Exists(dataDir) ? Directory.EnumerateFiles(dataDir, "*.mp4")
        .Select(Get)
        .Union(Directory.EnumerateFiles(dataDir, "*.mjpeg").Select(Get))
        .Where(x => x != null)
        .OrderByDescending(x => x.Started) : Array.Empty<Recording>();
    }
    private static Recording Get(string fullName, Regex regex)
    {
        var fileName = Path.GetFileName(fullName);
        Match match = regex.Match(fileName);

        if (match.Success)
        {
            string videoName = match.Groups[1].Value;
            string year = match.Groups[2].Value;
            string month = match.Groups[3].Value;
            string day = match.Groups[4].Value;
            string optionalDays = match.Groups[5].Value;
            string hour = match.Groups[6].Value;
            string minute = match.Groups[7].Value;
            string second = match.Groups[8].Value;
            string duration = match.Groups[9].Value;

            DateTime date = new DateTime(int.Parse(year), int.Parse(month), int.Parse(day));
            TimeSpan time = new TimeSpan(int.Parse(hour), int.Parse(minute), int.Parse(second));

            if (!string.IsNullOrEmpty(optionalDays))
                time = time.Add(TimeSpan.FromDays(int.Parse(optionalDays)));
            date += time;
            FileInfo finto = new FileInfo(fullName);
            return new Recording(fileName, fullName, videoName, date, TimeSpan.FromSeconds(int.Parse(duration)), finto.Length);
        }

        return null;
    }

    private string? _displayName;
    private bool _displayNameLoaded;
    public string DisplayName
    {
        get
        {
            if (_displayNameLoaded)
                return _displayName;
            string metadata = FullPath + ".name";
            _displayName = File.Exists(metadata) ? File.ReadAllText(metadata) : Name;
            _displayNameLoaded = true;
            return _displayName;
        }
        set
        {
            string metadata = FullPath + ".name";
            File.WriteAllText(metadata, value);
            _displayName = value;
        }
    }
    public void Delete()
    {
        File.Delete(FullPath);
        string metadata = FullPath + ".name";
        if (File.Exists(metadata))
            File.Delete(metadata);
    }
}
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ModelingEvolution.VideoStreaming.LibJpegTurbo;

public static class JpegEncoderFactory
{
    private static bool _initialized = false;
    public static JpegEncoder Create(int width, int height, int quality, ulong minimumBufferSize)
    {
        Debug.WriteLine("Creating JpegEncoder...");

        if (_initialized) 
            return new JpegEncoder(width, height, quality, minimumBufferSize);

        lock (typeof(JpegEncoderFactory))
        {
            if (_initialized)
                return new JpegEncoder(width, height, quality, minimumBufferSize);

            var currentDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
            string? srcDir = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                srcDir = Path.Combine(currentDir, "libs", "win");
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var arch = RuntimeInformation.OSArchitecture.ToString().ToLower();
                srcDir = Path.Combine(currentDir, "libs", $"linux-{arch}");
            }
            else throw new PlatformNotSupportedException();
            if (Directory.Exists(srcDir))
                foreach (var i in Directory.GetFiles(srcDir))
                {
                    var dstPath = Path.Combine(currentDir, Path.GetFileName(i))
                        .Replace(".so", ".dll");
                    FileInfo src = new FileInfo(i);
                    FileInfo dst = new FileInfo(dstPath);
                    if (!dst.Exists || dst.Length != src.Length || dst.CreationTime != src.CreationTime)
                    {
                        File.Copy(i, dstPath, true);
                        File.SetCreationTime(dstPath, src.CreationTime);
                    }
                }

            _initialized = true;
        }
        return new JpegEncoder(width, height, quality, minimumBufferSize);
    }
}
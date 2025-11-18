using System.Runtime.InteropServices;

namespace ModelingEvolution.VideoStreaming;

public static class ThreadUtils
{
    [DllImport("libc")]
    private static extern int gettid();

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    public static uint GetThreadId()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetCurrentThreadId();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return (uint)gettid();

        } 
        else throw new NotSupportedException();
    }
}

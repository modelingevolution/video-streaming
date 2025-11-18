using System.Runtime.InteropServices;

namespace ModelingEvolution.VideoStreaming;

public static class ThreadAffinity
{
    // Windows
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll")]
    private static extern IntPtr SetThreadAffinityMask(IntPtr hThread, IntPtr dwThreadAffinityMask);

    // Linux
    private const int CPU_SETSIZE = 1024;
    private const int __CPU_BITS = 8 * sizeof(ulong);
    [StructLayout(LayoutKind.Sequential)]
    public struct cpu_set_t
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = CPU_SETSIZE / __CPU_BITS)]
        public ulong[] _bits;

        public cpu_set_t()
        {
            _bits = new ulong[CPU_SETSIZE / __CPU_BITS];
        }

        public void Zero()
        {
            for (int i = 0; i < _bits.Length; i++)
                _bits[i] = 0;
        }

        public void Set(int cpu)
        {
            _bits[cpu / __CPU_BITS] |= 1UL << (cpu % __CPU_BITS);
        }
    }


    [DllImport("libc")]
    private static extern int sched_setaffinity(int pid, IntPtr cpusetsize, ref cpu_set_t mask);

    [DllImport("libc")]
    private static extern int sched_getaffinity(int pid, IntPtr cpusetsize, ref cpu_set_t mask);

    public unsafe static void SetAffinity(int processorIndex)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            IntPtr thread = GetCurrentThread();
            IntPtr mask = new IntPtr(1 << processorIndex);
            SetThreadAffinityMask(thread, mask);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var mask = new cpu_set_t();
            mask.Zero();
            mask.Set(processorIndex);
            int pid = 0; // 0 means current thread

            if (sched_setaffinity(pid, (IntPtr)sizeof(cpu_set_t), ref mask) != 0)
            {
                throw new InvalidOperationException("Error setting thread affinity");
            }
        }
        else
        {
            throw new PlatformNotSupportedException("This platform is not supported");
        }
    }
}

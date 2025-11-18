using System.Runtime.CompilerServices;

namespace ModelingEvolution.VideoStreaming.Yolo;

static class ArrayOperations
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static byte AvgGreaterThan(this byte[] array, byte threshold, int offset, int count)
    {
        ulong value = 0;
        ulong c = 0;
        for (int i = 0; i < count; i++)
        {
            var tmp = array[offset + i];
            if (tmp < threshold) continue;
            
            value += tmp;
            c += 1;
        }
        return (byte)(value / c);
    }
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static unsafe byte AvgGreaterThan(byte* array, byte threshold, int offset, int count)
    {
        // TODO: should use Vector128 or Vector256
        ulong value = 0;
        ulong c = 0;
        for (int i = 0; i < count; i++)
        {
            var tmp = array[offset + i];
            if (tmp < threshold) continue;

            value += tmp;
            c += 1;
        }
        return (byte)(value / c);
    }
}
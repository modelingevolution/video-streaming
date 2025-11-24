using System.Drawing;
using System.Globalization;

namespace ModelingEvolution.VideoStreaming;

#pragma warning disable CS4014

using System;
public static class TimeSpanExtensions
{
    private static readonly string[] TimeUnits = { "ns", "μs", "ms", "s", "min", "h", "d", "y" };
    private static readonly double[] TimeDivisors = { 1, 1000, 1000, 1000, 60, 60, 24, 365 };
    public static string WithTimeSuffix(this TimeSpan timeSpan, int decimalPlaces = 1)
    {
        if (decimalPlaces < 0) throw new ArgumentOutOfRangeException(nameof(decimalPlaces));
        double totalNanoseconds = timeSpan.Ticks * 100; // 1 tick = 100 nanoseconds
        int mag = 0;
        while (mag < TimeUnits.Length - 1 && totalNanoseconds >= TimeDivisors[mag + 1])
        {
            totalNanoseconds /= TimeDivisors[mag + 1];
            mag++;
        }
        return string.Format("{0:n" + decimalPlaces + "} {1}", totalNanoseconds, TimeUnits[mag]);
    }
}

static class SizeExtensions
{
    static readonly string[] SizeSuffixes =
        { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

    public static string WithSizeSuffix(this int value, int decimalPlaces = 1)
    {
        return ((long)value).WithSizeSuffix(decimalPlaces);
    }

    
    public static string WithSizeSuffix(this ulong value, int decimalPlaces = 1)
    {
        if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
        if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

        // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
        int mag = (int)Math.Log(value, 1024);

        // 1L << (mag * 10) == 2 ^ (10 * mag) 
        // [i.e. the number of bytes in the unit corresponding to mag]
        decimal adjustedSize = (decimal)value / (1L << (mag * 10));

        // make adjustment when the value is large enough that
        // it would round up to 1000 or more
        if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
        {
            mag += 1;
            adjustedSize /= 1024;
        }

        return string.Format("{0:n" + decimalPlaces + "} {1}",
            adjustedSize,
            SizeSuffixes[mag]);
    }
    public static string WithSizeSuffix(this long value, int decimalPlaces = 1)
    {
        if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException("decimalPlaces"); }
        if (value < 0) { return "-" + WithSizeSuffix(-value, decimalPlaces); }
        if (value == 0) { return string.Format("{0:n" + decimalPlaces + "} bytes", 0); }

        // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
        int mag = (int)Math.Log(value, 1024);

        // 1L << (mag * 10) == 2 ^ (10 * mag) 
        // [i.e. the number of bytes in the unit corresponding to mag]
        decimal adjustedSize = (decimal)value / (1L << (mag * 10));

        // make adjustment when the value is large enough that
        // it would round up to 1000 or more
        if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
        {
            mag += 1;
            adjustedSize /= 1024;
        }

        return string.Format("{0:n" + decimalPlaces + "} {1}",
            adjustedSize,
            SizeSuffixes[mag]);
    }
}
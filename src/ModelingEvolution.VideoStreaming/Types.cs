using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelingEvolution.VideoStreaming
{
    public enum VideoTransport : int
    {
        Tcp, Udp, Shm
    }

    public enum VideoSource
    {
        Camera,
        File, 
        Stream
    }
    public enum VideoCodec : int
    {
        Mjpeg, H264
    }

    public enum VideoResolution : int
    {
        FullHd, SubHd
    }
    public readonly record struct Resolution(int Width, int Height) : IParsable<Resolution>
    {
        public static readonly Resolution FullHd = new Resolution(1920, 1080);
        public static readonly Resolution SubHd = new Resolution(1456, 1088);
        public override string ToString()
        {
            return $"{Width}x{Height}";
        }

        public static Resolution Parse(string s, IFormatProvider? provider)
        {
            var segments = s.Split('x');
            return new Resolution(int.Parse(segments[0]), int.Parse(segments[1]));
        }

        public static bool TryParse(string? s, out Resolution result) =>
            Resolution.TryParse(s, null, out result);

        public static bool TryParse(string? s, IFormatProvider? provider, out Resolution result)
        {
            if (s == null)
            {
                result = default;
                return false;
            }
            var segments = s.Split('x');
            if (segments.Length != 2)
            {
                result = default;
                return false;
            }
            if (int.TryParse(segments[0], out var w) && int.TryParse(segments[1], out var h))
            {
                result = new Resolution(w, h);
                return true;
            }
            result = default;
            return false;
        }
    }
}

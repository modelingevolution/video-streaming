namespace ModelingEvolution.VideoStreaming.Player;

public sealed class MjpegDecoder
{
    private Func<byte, JpegMarker?> _state;

    public MjpegDecoder() => _state = DetectStart_1;
    public static bool IsJpeg(Memory<byte> frame)
    {
        if (frame.Span[0] == 0xFF || frame.Span[1] == 0xD8)
        {
            var last = frame.Length - 2;
            if (frame.Span[last] == 0xFF && frame.Span[last + 1] == 0xD9)
                return true;
        }

        return false;
    }
    private JpegMarker? DetectStart_1(byte b)
    {
        if (b == 0xFF)
            _state = DetectStart_2;
        return null;
    }

    private JpegMarker? DetectStart_2(byte b)
    {
        if (b == 0xD8)
            _state = DetectEnd_1;
        else
        {
            _state = DetectStart_1;
            return null;
        }
        return JpegMarker.Start;
    }

    private JpegMarker? DetectEnd_1(byte b)
    {
        if (b == 0xFF)
            _state = DetectEnd_2;

        return null;
    }
    private JpegMarker? DetectEnd_2(byte b)
    {
        if (b == 0xD9)
            _state = DetectStart_1;
        else
        {
            _state = DetectEnd_1;
            return null;
        }
        return JpegMarker.End;
    }
    public JpegMarker Decode(byte nx) => _state(nx) ?? JpegMarker.None;
}
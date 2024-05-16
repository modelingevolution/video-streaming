namespace ModelingEvolution.VideoStreaming;

public sealed class ReverseMjpegDecoder
{
    private Func<byte, JpegMarker> _state;

    public ReverseMjpegDecoder() => _state = DetectStart_1;

    private JpegMarker DetectStart_1(byte b)
    {
        if (b == 0xD8)
            _state = DetectStart_2;
        return JpegMarker.None;
    }

    private JpegMarker DetectStart_2(byte b)
    {
        if (b == 0xFF)
            _state = DetectEnd_1;
        else
        {
            _state = DetectStart_1;
            return JpegMarker.None;
        }
        return JpegMarker.Start;
    }
    private JpegMarker DetectEnd_1(byte b)
    {
        if (b == 0xD9)
            _state = DetectEnd_2;
        else
        {
            _state = DetectEnd_1;
        }
        
        return JpegMarker.None;
    }
    private JpegMarker DetectEnd_2(byte b)
    {
        if (b == 0xFF)
            _state = DetectStart_1;

        return JpegMarker.End;
    }
    public JpegMarker Decode(byte nx) => _state(nx);
}
public  sealed class MjpegDecoder
{
    private Func<byte, JpegMarker?> _state;

    public MjpegDecoder() => _state = DetectStart_1;

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
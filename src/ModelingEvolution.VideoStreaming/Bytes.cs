namespace ModelingEvolution.VideoStreaming;

#pragma warning disable CS4014
public readonly struct Bytes
{
    private readonly string _text;
    private readonly long _value;
    private readonly sbyte _precision;
    private Bytes(long value, sbyte precision=1)
    {
        _value = value;
        _text = value.WithSizeSuffix(precision);
        _precision = precision;
    }
    public static implicit operator Bytes(ulong value)
    {
        return new Bytes((long)value);
    }
    public static implicit operator Bytes(long value)
    {
        return new Bytes(value);
    }

    public override string ToString() => _text;
}
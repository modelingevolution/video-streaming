using System.Text.Json.Serialization;
using System.Text.Json;
using ProtoBuf;

namespace ModelingEvolution.VideoStreaming;

#pragma warning disable CS4014

public class BytesJsonConverter : JsonConverter<Bytes>
{
    public override Bytes Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            long value = reader.GetInt64();
            return new Bytes(value);
        }

        throw new JsonException($"Unexpected token {reader.TokenType} when parsing Bytes");
    }

    public override void Write(Utf8JsonWriter writer, Bytes value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue((long)value);
    }
}
[JsonConverter(typeof(BytesJsonConverter))]
[ProtoContract]
public record struct Bytes
{
    private string? _text;
    [ProtoMember(1, IsRequired = true)]
    private readonly long _value;
    private readonly sbyte _precision;
    internal Bytes(long value, sbyte precision=1)
    {
        _value = value;
        _text = value.WithSizeSuffix(precision);
        _precision = precision;
    }

    private string Text
    {
        get
        {
            if (_text != null) return _text;
            _text = _value.WithSizeSuffix(_precision);
            return _text;
        }
    }
    public static Bytes operator +(Bytes a, Bytes b)
    {
        return new Bytes(a._value + b._value, a._precision);
    }
    public static Bytes operator -(Bytes a, Bytes b)
    {
        return new Bytes(a._value - b._value, a._precision);
    }
    public static implicit operator Bytes(ulong value) => new((long)value);
    public static implicit operator Bytes(long value) => new(value);
    public static implicit operator Bytes(int value) => new(value);
    public static implicit operator long(Bytes value) => value._value;
    public override string ToString() => Text;
}
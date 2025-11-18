using System.Text;

namespace ModelingEvolution.VideoStreaming;

#pragma warning disable CS4014
public static class Extensions
{
    public static async Task WritePrefixedAsciiString(this Stream stream, string value)
    {
        var name = Encoding.ASCII.GetBytes(value);
        stream.WriteByte((byte)name.Length);
        await stream.WriteAsync(name);
    }
}
using System.Text;

namespace ModelingEvolution.VideoStreaming.Player
{
    public readonly struct JpegFrame
    {
        public readonly byte[] Data { get; init; }
        public readonly int Length { get; init; }
    }
    public static class Extensions
    {
        public static byte[] Swap(this byte[] c, byte[] a, byte[] b) => a == c ? b : a;
        public static async Task WritePrefixedAsciiString(this Stream stream, string value)
        {
            var name = Encoding.ASCII.GetBytes(value);
            stream.WriteByte((byte)name.Length);
            await stream.WriteAsync(name);
        }
    }
}

using System.Buffers;
using System.Text;
using ModelingEvolution.VideoStreaming.Buffers;

namespace ModelingEvolution.VideoStreaming.Player
{
    public readonly struct JpegFrame
    {
        public readonly Memory<byte> Data { get; init; }
        public readonly ulong FrameNumber { get; init; }

        public JpegFrame(ulong frameNumber, Memory<byte> data)
        {
            Data = data;
            FrameNumber = frameNumber;
        }
    }
    public readonly struct ManagedJpegFrame : IDisposable
    {
        private readonly ManagedArray<byte> _buffer;

        public readonly byte[] Data => _buffer.GetBuffer();
        public readonly ulong FrameNumber { get;  }

        public ManagedJpegFrame(ulong frameNumber, ManagedArray<byte> buffer)
        {
            _buffer = buffer;

            
            FrameNumber = frameNumber;
        }

        public void Dispose()
        {
            _buffer.Dispose();
        }
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

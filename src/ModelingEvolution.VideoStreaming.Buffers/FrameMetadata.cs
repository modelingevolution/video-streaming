using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace ModelingEvolution.VideoStreaming.Buffers
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct FrameMetadata(ulong frameNumber, ulong frameSize, ulong streamPosition)
    {
        // Frame number, 0,1,2...
        public readonly ulong FrameNumber = frameNumber;
        public readonly ulong FrameSize = frameSize;

        // Stream position, 0, {FrameSize}, 2x{FrameSize} ...
        public readonly ulong StreamPosition = streamPosition;
        private readonly ulong _xor = frameNumber ^ frameSize ^ streamPosition;

        public bool IsOk => FrameSize > 0 && _xor == (FrameNumber ^ FrameSize ^ StreamPosition);
    }
}

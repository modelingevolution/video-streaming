using System.Runtime.CompilerServices;

namespace ModelingEvolution.VideoStreaming;

#pragma warning disable CS4014
public static class FrameBasedMultiplexerExtensions
{
    public static async IAsyncEnumerable<Memory<byte>> Read(this IFrameBasedMultiplexer b, 
        int fps=30, 
        [EnumeratorCancellation] CancellationToken token = default)
    {
        TimeSpan w = TimeSpan.FromSeconds(1d / (fps+fps));
        int offset = b.ReadOffset;
        var buffer = b.Buffer();
        await Task.Delay(w*2, token); // min. ~1 frame

        while (!token.IsCancellationRequested)
        {
            var noffset = b.ReadOffset;
            var d = noffset - offset;
            if (d > 0)
            {
                yield return buffer.Slice(offset, d);
                offset = noffset;
            }
            else if(d < 0)
            {
                yield return buffer.Slice(offset, b.Padding - offset);
                yield return buffer.Slice(0, noffset);
                offset = noffset;
            }
            else
            {
                await Task.Delay(w, token);
            }
        }
    }
}
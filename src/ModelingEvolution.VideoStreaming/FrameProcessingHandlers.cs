using ModelingEvolution.VideoStreaming.Buffers;

namespace ModelingEvolution.VideoStreaming;

public static class FrameProcessingHandlers
{

    public static unsafe JpegFrame OnProcessHdr(YuvFrame frame,
        YuvFrame? prvFrame,
        ulong secquence,
        int pipeId,
        PipeProcessingState state,
        CancellationToken token)
    {

        var ptr = state.Buffer.GetPtr();

        ulong len = 0;
        var prv = prvFrame.HasValue ? (nint)prvFrame.Value.Data : (nint)IntPtr.Zero;
        Memory<byte> data = null;
        if (prv != IntPtr.Zero)
        {
            byte* src = frame.Data;
            byte* src2 = (byte*)prv;

            var mergePtr = state.MergeBufferPtr();
            for (int i = 0; i < state.MergeBuffer.Length; i++)
            {
                mergePtr[i] = (byte)((src[i] + src2[i]) >> 1);
            }


            len = state.Encoder.Encode((nint)mergePtr, (nint)state.Buffer.GetPtr(), state.Buffer.MaxObjectSize);
            data = state.Buffer.Use((uint)len);
            prv = (nint)src;

        }
        else
        {
            byte* src = frame.Data;
            len = state.Encoder.Encode((nint)src, (nint)state.Buffer.GetPtr(), state.Buffer.MaxObjectSize);
            data = state.Buffer.Use((uint)len);

        }


        var metadata = new FrameMetadata(frame.Metadata.FrameNumber, len, frame.Metadata.StreamPosition);
        return new JpegFrame(metadata, data);
    }
    public static unsafe JpegFrame OnProcess(YuvFrame frame,
        YuvFrame? prv,
        ulong secquence,
        int pipeId,
        PipeProcessingState state,
        CancellationToken token)
    {
        var ptr = state.Buffer.GetPtr();

        var len = state.Encoder.Encode((nint)frame.Data, (nint)ptr, state.Buffer.MaxObjectSize);
        var data = state.Buffer.Use((uint)len);
        var metadata = new FrameMetadata(frame.Metadata.FrameNumber, len, frame.Metadata.StreamPosition);
        return new JpegFrame(metadata, data);
    }
}

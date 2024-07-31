using Emgu.CV.CvEnum;
using Emgu.CV.Dai;
using Emgu.CV;
using ModelingEvolution.VideoStreaming.Buffers;
using System.Diagnostics;
using System.Drawing;
using Tmds.Linux;

namespace ModelingEvolution.VideoStreaming;

public static class FrameProcessingHandlers
{
    public static EventHandler<byte[]> OnFrameMerged;

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

        _ = Task.Run(() => OnFrameMerged?.Invoke(new object(), data.ToArray()));
        var metadata = new FrameMetadata(frame.Metadata.FrameNumber, len, frame.Metadata.StreamPosition);

        return new JpegFrame(metadata, data);
    }
    
    public static unsafe JpegFrame OnProcessHdrSimple(YuvFrame frame,
       YuvFrame? prv,
       ulong secquence,
       int pipeId,
       PipeProcessingState state,
       CancellationToken token)
    {
        var sw = Stopwatch.StartNew();
        var ptr = state.Buffer.GetPtr();

        var s = new Size(frame.Info.Width, frame.Info.Height);
        using Mat src = new Mat(s, DepthType.Cv8U, 3, (IntPtr)frame.Data, 0);

        IntPtr prvPtr = prv.HasValue ? (IntPtr)prv.Value.Data : (IntPtr)frame.Data;
        using Mat src2 = new Mat(s, DepthType.Cv8U, 3, prvPtr, 0);

        CvInvoke.AddWeighted(src, 0.5d, src2, 0.5d, 0, state.Dst, DepthType.Cv8U);

        var len = state.Encoder.Encode(state.Dst.DataPointer, (nint)ptr, state.Buffer.MaxObjectSize);
        var data = state.Buffer.Use((uint)len);

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

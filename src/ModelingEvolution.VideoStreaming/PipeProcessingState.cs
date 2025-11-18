using System.Buffers;
using Emgu.CV;
using ModelingEvolution.VideoStreaming.LibJpegTurbo;
using Emgu.CV.CvEnum;
using System.Drawing;

namespace ModelingEvolution.VideoStreaming;

public class PipeProcessingState
{
    private int _quality = 80;
    
    public JpegEncoder Encoder { get; }
    public MergeMertens MergeMertens = new MergeMertens(1, 0, 0);
    public CyclicMemoryBuffer Buffer { get; }
    public int Quality 
    { 
        get => _quality; 
        set {
            if(_quality == value) return;
            Encoder.Quality = value;
            _quality = value; 
        } 
    }
    public Mat Dst { get; }

    public readonly byte[] MergeBuffer;
    public readonly MemoryHandle MergeMemHandle;
    public unsafe byte* MergeBufferPtr() => (byte*)MergeMemHandle.Pointer;
    public PipeProcessingState(int w, int h, uint frameSize, uint count)
    {
        Encoder = JpegEncoderFactory.Create(w,h, Quality, 0);
        Buffer = new CyclicMemoryBuffer(count, frameSize);
        Dst = new Mat(new Size(w,h), DepthType.Cv8U, 3);

        this.MergeBuffer = new byte[w * h * 3 / 2];
        Memory<byte> mem = new Memory<byte>(MergeBuffer);
        MergeMemHandle = mem.Pin();
    }
}

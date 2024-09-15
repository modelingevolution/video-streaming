using System.Net.WebSockets;

namespace ModelingEvolution.VideoStreaming.VectorGraphics;

public class RemoteStreamingCanvasClient
{
    private readonly RemoteCanvas _canvas;
    private readonly WebSocketFrameWriter _writer;
    public ICanvas Canvas => _canvas;
    private ulong _frame = 1;
    public ulong Frame => _frame;
    public RemoteStreamingCanvasClient(WebSocket socket)
    {
        _writer = new WebSocketFrameWriter(socket);
        _canvas = new RemoteCanvas(OnPush, OnComplete);
        _writer.WriteFrameNumber(_frame).GetAwaiter().GetResult();
    }

    private void OnComplete()
    {
        _writer.WriteFrameEnd()
            .ContinueWith(_ => _writer.WriteFrameNumber(++_frame))
            .GetAwaiter().GetResult();
    }

    private void OnPush(IRenderOp obj)
    {
        _writer.WriteFramePayload(1, obj).GetAwaiter().GetResult();
    }
}
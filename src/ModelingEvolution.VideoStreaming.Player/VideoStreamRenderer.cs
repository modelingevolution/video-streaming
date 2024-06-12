using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using SkiaSharp;

namespace ModelingEvolution.VideoStreaming.Player
{
    public class VideoStreamRenderer
    {
        public bool IsConnected { get; private set; }
        private CancellationTokenSource cts;
        private readonly ConcurrentQueue<JpegFrame> _queue = new();
        public void Disconnect()
        {
            if(IsConnected)
                cts?.Cancel();
        }
        public VideoStreamRenderer Connect(string host, int port, string streamName = "localhost")
        {
            if (IsConnected) return this;
            IsConnected = true;
            cts = new CancellationTokenSource();
            Task.Factory.StartNew(async () =>
            {
                try
                {
                    using TcpClient client = new TcpClient(host, port);
                    var stream = client.GetStream();
                    await stream.WritePrefixedAsciiString(streamName);
                    await stream.Copy(_queue, token: cts.Token);
                }
                catch
                {
                    IsConnected = false;
                }
            }, TaskCreationOptions.LongRunning);
            return this;
        }
        
        public void Render(SKCanvas canvas)
        {
            while (_queue.TryDequeue(out var frame))
            {
                using (var image = SKImage.FromEncodedData(frame.Data.AsSpan(0, frame.Length)))
                {
                    if (image != null)
                        canvas.DrawImage(image, new SKRect(0, 0, image.Width, image.Height));
                    Debug.WriteLine("Null");
                }
                ArrayPool<byte>.Shared.Return(frame.Data);
            }
        }
    }
}

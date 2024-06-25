using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using ModelingEvolution.VideoStreaming.Buffers;
using SkiaSharp;

namespace ModelingEvolution.VideoStreaming.Player
{
    public class VideoStreamRenderer
    {
        public bool IsConnected { get; private set; }
        private CancellationTokenSource cts;
        private readonly ConcurrentQueue<Memory<byte>> _queue = new();
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
                    await stream.Copy2(_queue, token: cts.Token);
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
                using (var image = SKImage.FromEncodedData(frame.Span))
                {
                    if (image != null)
                        canvas.DrawImage(image, new SKRect(0, 0, image.Width, image.Height));
                    Debug.WriteLine("Null");
                }
            }
        }
    }
}

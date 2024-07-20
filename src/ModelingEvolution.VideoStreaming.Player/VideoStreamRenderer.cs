using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Maui.Dispatching;
using ModelingEvolution.VideoStreaming.Buffers;
using SkiaSharp;

namespace ModelingEvolution.VideoStreaming.Player
{
    public readonly record struct FrameSurface
    {
        public readonly SKSurface Surface;
        public readonly ulong Id;

        public FrameSurface(ulong id, SKSurface surface)
        {
            Id = id; Surface = surface;
        }
    }
    public class VideoStreamRenderer
    {
        public bool IsConnected { get; private set; }
        private CancellationTokenSource cts;

        private readonly ConcurrentStack<SKSurface> _surfaces = new();
        private readonly Channel<FrameSurface> _chSurfaces;
        private ulong droppedFrames, renderedFrames, currentFrameId, unorderedFrames = 0;
        
        private DateTime started;

        public VideoStreamRenderer()
        {
            _chSurfaces = Channel.CreateBounded<FrameSurface>(new BoundedChannelOptions(1));
            for (int i = 0; i < 8; i++)
                _surfaces.Push(SKSurface.Create(new SKImageInfo(1920, 1080)));
        }
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

                    await Parallel.ForEachAsync(stream.GetFrames(), async (frame, ct) =>
                    {
                        if (_surfaces.TryPop(out var surface))
                        {
                            using var image = SKImage.FromEncodedData(frame.Data.Span);
                            surface.Canvas.DrawImage(image, 0, 0);
                            _chSurfaces.Writer.TryWrite(new FrameSurface(frame.FrameNumber, surface));
                        }
                    });
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
            if (!_chSurfaces.Reader.TryRead(out var frame)) return;
            if (renderedFrames == 0) started = DateTime.Now;
            if (currentFrameId > frame.Id)
            {
                unorderedFrames += 1;
                _surfaces.Push(frame.Surface);
                return;
            }
            canvas.DrawSurface(frame.Surface, 0, 0);

            renderedFrames += 1;
            currentFrameId = frame.Id;
            _surfaces.Push(frame.Surface);
        }
    }
}

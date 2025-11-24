using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using ModelingEvolution.VideoStreaming.VectorGraphics;
using Polygon = ModelingEvolution.VideoStreaming.VectorGraphics.Polygon;

namespace ModelingEvolution.VideoStreaming.Player.Wpf
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private readonly ConcurrentStack<SKSurface> _surfaces = new();
        private readonly Channel<FrameSurface> _chSurfaces;
        private ulong droppedFrames, renderedFrames, currentFrameId, unorderedFrames = 0;
        private DateTime started;
        private DispatcherTimer drawTimer;
        private readonly StreamingCanvasEngine _engine;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnCanvasLoaded;
            _chSurfaces = Channel.CreateBounded<FrameSurface>(new BoundedChannelOptions(1)
            { FullMode = BoundedChannelFullMode.DropOldest }, OnReturnSurfaceDropped);

            for (int i = 0; i < 8; i++)
                _surfaces.Push(SKSurface.Create(new SKImageInfo(1920, 1080)));

            this._engine = new StreamingCanvasEngine(new LoggerFactory());

            _ = _engine.ConnectAsync(new Uri($"ws://localhost:5197/vector-stream/default"));
            
            //Task.Factory.StartNew(async () =>
            //{
            //    try
            //    {
            //        using TcpClient client = new TcpClient("pi-200", 7000);
            //        var stream = client.GetStream();
            //        await stream.WritePrefixedAsciiString("a");

            //        await Parallel.ForEachAsync(stream.GetFrames(), async (frame, ct) =>
            //        {
            //            if (_surfaces.TryPop(out var surface))
            //            {
            //                using var image = SKImage.FromEncodedData(frame.Data.Span);
            //                surface.Canvas.DrawImage(image, 0, 0);
            //                _chSurfaces.Writer.TryWrite(new FrameSurface(frame.FrameNumber, surface));
            //            }
            //        });
            //    }
            //    catch(Exception ex) {
            //        MessageBox.Show(ex.Message);
            //    }
            //});
        }
        private void Refresh(object sender, EventArgs args)
        {

            this.canvas.InvalidateVisual();
            var sec = DateTime.Now.Subtract(started).TotalSeconds;
            if(sec > 0)
                this.lb.Text = $"Fps: {renderedFrames / sec:F1}, dropped: {droppedFrames}, unordered: {unorderedFrames}";

        }
        
        private void OnCanvasLoaded(object? sender, EventArgs e)
        {
            drawTimer = new DispatcherTimer(); // Roughly 60 FPS
            drawTimer.Interval = TimeSpan.FromMilliseconds(1);
            drawTimer.Tick += Refresh;
            drawTimer.Start();
        }
        private void OnPaint3(object? sender, SKPaintSurfaceEventArgs e)
        {
            _engine.Render(e.Surface.Canvas);
            return;

            if (!_chSurfaces.Reader.TryRead(out var frame)) return;
            if (renderedFrames == 0) started = DateTime.Now;
            if (currentFrameId > frame.Id)
            {
                unorderedFrames += 1;
                _surfaces.Push(frame.Surface);
                return;
            }
            e.Surface.Canvas.DrawSurface(frame.Surface, 0, 0);

            renderedFrames += 1;
            currentFrameId = frame.Id;
            _surfaces.Push(frame.Surface);
        }
        private void OnReturnSurfaceDropped(FrameSurface s)
        {
            _surfaces.Push(s.Surface);
            droppedFrames += 1;
        }
    }
}
using SkiaSharp;
using System.Buffers;
using SkiaSharp.Views.Maui;
using Microsoft.Maui.Dispatching;

namespace ModelingEvolution.VideoStreaming.Player.App
{
    public partial class MainPage : ContentPage
    {
        int count = 0;
        private IDispatcherTimer drawTimer;
        private readonly VideoStreamRenderer _renderer = new();
        public MainPage()
        {
            
            InitializeComponent();
            canvas.PaintSurface += OnCanvasViewPaintSurface;
            //_renderer.Connect()
            _renderer.Connect("pi-200", 7000, "a");

            drawTimer = this.Dispatcher.CreateTimer();
            drawTimer.Interval = TimeSpan.FromMilliseconds(1);
            drawTimer.Tick += OnRefresh;
            drawTimer.Start();
        }

        private void OnRefresh(object? sender, EventArgs e)
        {
            this.canvas.InvalidateSurface();
        }

        private void OnCanvasViewPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
        {
           _renderer.Render(e.Surface.Canvas);
        }
        private void OnCounterClicked(object sender, EventArgs e)
        {
            count++;

            if (count == 1)
                CounterBtn.Text = $"Clicked {count} time";
            else
                CounterBtn.Text = $"Clicked {count} times";

            SemanticScreenReader.Announce(CounterBtn.Text);
        }
    }

}

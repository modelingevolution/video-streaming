using SkiaSharp;

namespace ModelingEvolution.VideoStreaming.Player.Wpf
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
}
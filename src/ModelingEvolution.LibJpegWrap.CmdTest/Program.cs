using ModelingEvolution.VideoStreaming.LibJpegTurbo;

namespace ModelingEvolution.LibJpegWrap.CmdTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Testing loading");
            var encoder = JpegEncoderFactory.Create(1920, 1080, 80, 1920 * 1080 * 3 / 2);
            Console.WriteLine("Encoder created successfully");
            encoder.Dispose();
        }
    }
}

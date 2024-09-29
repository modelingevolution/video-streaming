namespace ModelingEvolution.VideoStreaming.Tests;


public class BitConverterTests
{
    [Fact]
    public void ToUInt64_7bytes()
    {
        byte[] data = new byte[] { 0xF, 0xF, 0xF };
        BitConverter.ToUInt64(data);
    }
}
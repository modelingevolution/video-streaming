using FluentAssertions.Execution;

namespace ModelingEvolution.VideoStreaming.Tests;

public static class MemoryAssertions {
    public static void ShouldBe(this Memory<byte> actual, Memory<byte> expected)
    {
        if (actual.Length != expected.Length)
            throw new AssertionFailedException($"Buffer lengths are different. Should be {expected.Length} but was {actual.Length}.");
        for (int i = 0; i < actual.Length; i++)
        {
            if (actual.Span[i] != expected.Span[i])
            {
                throw new AssertionFailedException($"Buffers are different at {i}, {actual.Span[i]} should be {expected.Span[i]}");
            }
        }
    }
}
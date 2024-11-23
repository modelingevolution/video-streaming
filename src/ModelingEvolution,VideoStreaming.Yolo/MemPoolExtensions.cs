using System.Buffers;

namespace ModelingEvolution_VideoStreaming.Yolo;

internal static class MemPoolExtensions
{
    class MemoryOwner<T>(IMemoryOwner<T> inner, int size) : IMemoryOwner<T>
    {
        public void Dispose() => inner.Dispose();

        public Memory<T> Memory => inner.Memory.Slice(0, size);
    }
    public static IMemoryOwner<T> Exact<T>(this IMemoryOwner<T> m, int size)
    {
        return new MemoryOwner<T>(m, size);
    }
    public static DenseTensorOwner<T> AllocateTensor<T>(this MemoryPool<T> allocator,
        in TensorShape shape, bool clean = false)
    {
        var mOwn = allocator.Rent(shape.Length).Exact(shape.Length);
        if (clean) mOwn.Memory.Span.Fill(default(T));
        return new DenseTensorOwner<T>(mOwn, shape.Dimensions);
    }
}
namespace ModelingEvolution.VideoStreaming;

static class CollectionExtensions
{
    public static void SafeAddUnique<T>(this IList<T> collection, T item)
    {
        lock (collection)
        {
            if (!collection.Contains(item))
                collection.Add(item);
        }
    }

    public static bool SafeRemove<T>(this IList<T> collection, T item)
    {
        lock (collection)
        {
            return collection.Remove(item);
        }
    }
}
using System.Collections;

namespace ModelingEvolution.VideoStreaming.Buffers;

/// <summary>
/// A custom dictionary implementation using a sorted list with binary search for efficient key lookups.
/// </summary>
/// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
public class SortedListDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IDisposable
{
    // Internal list to store key-value pairs
    private readonly ManagedArray<KeyValuePair<TKey, TValue>> _items;

    // Comparer to maintain sorting and perform binary search
    private readonly IComparer<TKey> _comparer;
    private readonly IComparer<KeyValuePair<TKey, TValue>> _collectionComparer;
    /// <summary>
    /// Initializes a new instance of the SortedListDictionary class.
    /// </summary>
    public SortedListDictionary() : this(Comparer<TKey>.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the SortedListDictionary class with a custom comparer.
    /// </summary>
    /// <param name="comparer">The comparer to use for sorting and searching keys.</param>
    public SortedListDictionary(IComparer<TKey> comparer)
    {
        _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
        _collectionComparer = Comparer<KeyValuePair<TKey, TValue>>.Create((x, y) => _comparer.Compare(x.Key, y.Key));
        _items = new ManagedArray<KeyValuePair<TKey, TValue>>();
    }

    /// <summary>
    /// Gets the number of key/value pairs in the dictionary.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Gets a value indicating whether the dictionary is read-only.
    /// </summary>
    public bool IsReadOnly => false;

    /// <summary>
    /// Gets the collection of keys in the dictionary.
    /// </summary>
    public ICollection<TKey> Keys => _items.Select(kvp => kvp.Key).ToList();

    /// <summary>
    /// Gets the collection of values in the dictionary.
    /// </summary>
    public ICollection<TValue> Values => _items.Select(kvp => kvp.Value).ToList();

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the value to get or set.</param>
    /// <returns>The value associated with the specified key.</returns>
    public TValue this[TKey key]
    {
        get
        {
            int index = BinarySearch(key);
            if (index < 0)
                throw new KeyNotFoundException($"Key {key} not found in dictionary.");
            return _items[index].Value;
        }
        set
        {
            int index = BinarySearch(key);
            if (index < 0)
            {
                // Key not found, insert new key-value pair
                index = ~index;
                _items.Insert(index, new KeyValuePair<TKey, TValue>(key, value));
            }
            else
            {
                // Key found, update value
                _items[index] = new KeyValuePair<TKey, TValue>(key, value);
            }
        }
    }

    /// <summary>
    /// Adds a key/value pair to the dictionary.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <param name="value">The value to add.</param>
    public void Add(TKey key, TValue value)
    {
        int index = BinarySearch(key);
        if (index >= 0)
            throw new ArgumentException($"Key {key} already exists in dictionary.");

        // Insert at the correct sorted position
        index = ~index;
        _items.Insert(index, new KeyValuePair<TKey, TValue>(key, value));
    }

    /// <summary>
    /// Adds a key/value pair to the dictionary.
    /// </summary>
    /// <param name="item">The key/value pair to add.</param>
    public void Add(KeyValuePair<TKey, TValue> item)
    {
        Add(item.Key, item.Value);
    }

    /// <summary>
    /// Removes the key/value pair with the specified key from the dictionary.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns>True if the key was found and removed; otherwise, false.</returns>
    public bool Remove(TKey key)
    {
        int index = BinarySearch(key);
        if (index < 0)
            return false;

        _items.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Removes the first occurrence of a specific key/value pair from the dictionary.
    /// </summary>
    /// <param name="item">The key/value pair to remove.</param>
    /// <returns>True if the pair was found and removed; otherwise, false.</returns>
    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        int index = BinarySearch(item.Key);
        if (index < 0)
            return false;

        // Check if the value also matches
        if (!EqualityComparer<TValue>.Default.Equals(_items[index].Value, item.Value))
            return false;

        _items.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// Determines whether the dictionary contains a specific key.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns>True if the key is found; otherwise, false.</returns>
    public bool ContainsKey(TKey key)
    {
        return BinarySearch(key) >= 0;
    }

    /// <summary>
    /// Determines whether the dictionary contains a specific key/value pair.
    /// </summary>
    /// <param name="item">The key/value pair to locate.</param>
    /// <returns>True if the pair is found; otherwise, false.</returns>
    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        int index = BinarySearch(item.Key);
        if (index < 0)
            return false;

        return EqualityComparer<TValue>.Default.Equals(_items[index].Value, item.Value);
    }

    /// <summary>
    /// Tries to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">The key to find.</param>
    /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found.</param>
    /// <returns>True if the key was found; otherwise, false.</returns>
    public bool TryGetValue(TKey key, out TValue value)
    {
        int index = BinarySearch(key);
        if (index < 0)
        {
            value = default;
            return false;
        }

        value = _items[index].Value;
        return true;
    }

    /// <summary>
    /// Removes all key/value pairs from the dictionary.
    /// </summary>
    public void Clear()
    {
        _items.Clear();
    }

    /// <summary>
    /// Copies the elements of the dictionary to an array, starting at a particular array index.
    /// </summary>
    /// <param name="array">The one-dimensional array to copy elements to.</param>
    /// <param name="arrayIndex">The zero-based index at which copying begins.</param>
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        _items.CopyTo(array, arrayIndex);
    }

    /// <summary>
    /// Returns an enumerator that iterates through the dictionary.
    /// </summary>
    /// <returns>An enumerator for the dictionary.</returns>
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the dictionary.
    /// </summary>
    /// <returns>An enumerator for the dictionary.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Performs a binary search to find the index of a key in the sorted list.
    /// </summary>
    /// <param name="key">The key to search for.</param>
    /// <returns>
    /// The zero-based index of the key if found.
    /// A negative number that is the bitwise complement of the index of the next key that is larger than the search key,
    /// if the key is not found.
    /// </returns>
    private int BinarySearch(TKey key)
    {
        var v = new KeyValuePair<TKey, TValue>(key, default);
        
        return Array.BinarySearch(_items.GetBuffer(),0,this.Count,v,this._collectionComparer);
    }

    public void Dispose()
    {
        _items.Dispose();
    }
}
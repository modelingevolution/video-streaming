using ModelingEvolution.VideoStreaming.Buffers;

namespace ModelingEvolution.VideoStreaming.Tests;

public class ManagedArrayTests : IDisposable
{
    private ManagedArray<int> _managedArray;

    
    public ManagedArrayTests()
    {
        _managedArray = new ManagedArray<int>();
    }

    
    public void Dispose()
    {
        _managedArray.Dispose();
    }

    [Fact]
    public void Add_SingleElement_IncreasesCount()
    {
        _managedArray.Add(10);

        Assert.Equal(1, _managedArray.Count);
        Assert.Equal(10, _managedArray[0]);
    }

    [Fact]
    public void Add_MultipleElements_IncreasesCountCorrectly()
    {
        _managedArray.Add(1);
        _managedArray.Add(2);
        _managedArray.Add(3);

        Assert.Equal(3, _managedArray.Count);
        Assert.Equal(1, _managedArray[0]);
        Assert.Equal(2, _managedArray[1]);
        Assert.Equal(3, _managedArray[2]);
    }

    [Fact]
    public void Insert_AtSpecificIndex_ShiftsElements()
    {
        _managedArray.Add(1);
        _managedArray.Add(3);
        _managedArray.Insert(1, 2);

        Assert.Equal(3, _managedArray.Count);
        Assert.Equal(1, _managedArray[0]);
        Assert.Equal(2, _managedArray[1]);
        Assert.Equal(3, _managedArray[2]);
    }

    [Fact]
    public void RemoveAt_ValidIndex_RemovesElementAndShifts()
    {
        _managedArray.Add(1);
        _managedArray.Add(2);
        _managedArray.Add(3);

        _managedArray.RemoveAt(1);

        Assert.Equal(2, _managedArray.Count);
        Assert.Equal(1, _managedArray[0]);
        Assert.Equal(3, _managedArray[1]);
    }

    [Fact]
    public void Remove_ExistingElement_RemovesCorrectly()
    {
        _managedArray.Add(1);
        _managedArray.Add(2);
        _managedArray.Add(3);

        bool removed = _managedArray.Remove(2);

        Assert.True(removed);
        Assert.Equal(2, _managedArray.Count);
        Assert.Equal(1, _managedArray[0]);
        Assert.Equal(3, _managedArray[1]);
    }

    [Fact]
    public void Clear_EmptiesTheArray()
    {
        _managedArray.Add(1);
        _managedArray.Add(2);

        _managedArray.Clear();

        Assert.Equal(0, _managedArray.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = _managedArray[0]);
    }

    [Fact]
    public void Indexer_SetValue_UpdatesCorrectly()
    {
        _managedArray.Add(1);
        _managedArray[0] = 100;

        Assert.Equal(100, _managedArray[0]);
    }

    [Fact]
    public void Indexer_SetOutOfBounds_ExpandsArray()
    {
        _managedArray[3] = 42; // Expands to fit the index 3

        Assert.Equal(4, _managedArray.Count); // Size is now 4
        Assert.Equal(42, _managedArray[3]);
    }

    [Fact]
    public void Contains_ElementExists_ReturnsTrue()
    {
        _managedArray.Add(10);
        _managedArray.Add(20);

        Assert.True(_managedArray.Contains(20));
        Assert.False(_managedArray.Contains(30));
    }

    [Fact]
    public void CopyTo_CopiesElementsToArray()
    {
        _managedArray.Add(1);
        _managedArray.Add(2);

        int[] destination = new int[5];
        _managedArray.CopyTo(destination, 1);

        Assert.Equal(0, destination[0]); // Default value
        Assert.Equal(1, destination[1]);
        Assert.Equal(2, destination[2]);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        _managedArray.Dispose();
        _managedArray.Dispose();
    }
}
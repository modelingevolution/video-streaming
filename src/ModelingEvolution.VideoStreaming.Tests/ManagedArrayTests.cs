using ModelingEvolution.VideoStreaming.Buffers;

using System;
using Xunit;
using FluentAssertions;
using ModelingEvolution.VideoStreaming.VectorGraphics;
using NSubstitute;

namespace YourNamespace.Tests
{
    public class ManagedArrayStructTests
    {
        [Fact]
        public void Constructor_ShouldInitializeArrayWithGivenSize()
        {
            // Arrange
            int cap = 5;
            // Act
            var managedArray = new ManagedArrayStruct<int>(cap);
            // Assert
            managedArray.Capacity.Should().BeGreaterOrEqualTo(cap);
            managedArray.Count.Should().Be(0);
            for (int i = 0; i < cap; i++)
            {
                managedArray[i].Should().Be(default(int)); // Default value for int
            }
        }
        [Fact]
        public void Indexer_ShouldGetAndSetValuesCorrectly()
        {
            // Arrange
            var managedArray = new ManagedArrayStruct<string>(3);
            // Act
            managedArray[0] = "First";
            managedArray[1] = "Second";
            managedArray[2] = "Third";
            managedArray.Count.Should().Be(3);
            // Assert
            managedArray[0].Should().Be("First");
            managedArray[1].Should().Be("Second");
            managedArray[2].Should().Be("Third");
        }
       
        [Fact]
        public void Resize_ShouldChangeArraySizeAndPreserveExistingValues()
        {
            // Arrange
            var managedArray = new ManagedArrayStruct<int>(3);
            managedArray[0] = 1;
            managedArray[1] = 2;
            managedArray[2] = 3;
            managedArray.Count.Should().Be(3);
            
            managedArray[0].Should().Be(1);
            managedArray[1].Should().Be(2);
            managedArray[2].Should().Be(3);
            managedArray[3].Should().Be(default(int));
            managedArray[4].Should().Be(default(int));
        }
        
    }
}

public class DrawingBatchScopeTests
{
    [Fact]
    public void Add()
    {
        var canvas = NSubstitute.Substitute.For<ICanvas>();
        canvas.Sync.Returns(new object());
        DrawingBatchScope scope = new DrawingBatchScope(canvas, 4, 1);
        scope.DrawText("Foo");
        scope.Dispose();
        
        canvas.Received(1).DrawText(Arg.Any<string>(),0,0,12,null,4);
    }
}
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
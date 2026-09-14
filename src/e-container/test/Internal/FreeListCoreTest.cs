using System;
using System.Collections.Generic;
using Enaweg.Container.Internal;
using GdUnit4;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Internal;

[TestSuite]
public class FreeListCoreTest
{
    [TestCase]
    public void Add_ReturnsSequentialIndices_AndAsSpanReflectsItems()
    {
        var list = new FreeListCore<string>(new object());
        list.Add("a", out var keyA);
        list.Add("b", out var keyB);

        AssertInt(keyA).IsEqual(0);
        AssertInt(keyB).IsEqual(1);
        AssertArray(list.AsSpan().ToArray()).ContainsExactly("a", "b");
    }

    [TestCase]
    public void Remove_LastItem_ShrinksSpan()
    {
        var list = new FreeListCore<string>(new object());
        list.Add("a", out _);
        list.Add("b", out var keyB);

        list.Remove(keyB);

        AssertArray(list.AsSpan().ToArray()).ContainsExactly("a");
    }

    [TestCase]
    public void Remove_MiddleItem_LeavesNullSlot()
    {
        var list = new FreeListCore<string>(new object());
        list.Add("a", out _);
        list.Add("b", out var keyB);
        list.Add("c", out _);

        list.Remove(keyB);
        var span = list.AsSpan().ToArray();

        AssertInt(span.Length).IsEqual(3);
        AssertObject(span[1]).IsNull();
    }

    [TestCase]
    public void Remove_IndexWithinCapacityButUnassigned_ThrowsKeyNotFound()
    {
        // The backing array is over-allocated (grows to length 4 on the second Add), so index 2
        // is in-bounds but was never assigned - that's the only way Remove() throws.
        var list = new FreeListCore<string>(new object());
        list.Add("a", out _);
        list.Add("b", out _);

        AssertThrown(() => list.Remove(2)).IsInstanceOf<KeyNotFoundException>();
    }

    [TestCase]
    public void Remove_OutOfBoundsIndex_IsANoOp()
    {
        var list = new FreeListCore<string>(new object());
        list.Add("a", out _);

        list.Remove(5);

        AssertArray(list.AsSpan().ToArray()).ContainsExactly("a");
    }

    [TestCase]
    public void RemoveSlow_RemovesMatchingValue()
    {
        var list = new FreeListCore<string>(new object());
        list.Add("a", out _);
        list.Add("b", out _);

        var removed = list.RemoveSlow("b");

        AssertBool(removed).IsTrue();
        AssertArray(list.AsSpan().ToArray()).ContainsExactly("a");
    }

    [TestCase]
    public void RemoveSlow_MissingValue_ReturnsFalse()
    {
        var list = new FreeListCore<string>(new object());
        list.Add("a", out _);

        AssertBool(list.RemoveSlow("missing")).IsFalse();
    }

    [TestCase]
    public void Add_GrowsBeyondInitialCapacity()
    {
        var list = new FreeListCore<string>(new object());
        for (var i = 0; i < 10; i++)
        {
            list.Add($"item{i}", out _);
        }

        AssertInt(list.AsSpan().Length).IsEqual(10);
    }

    [TestCase]
    public void Clear_RemovesAllItems()
    {
        var list = new FreeListCore<string>(new object());
        list.Add("a", out _);
        list.Add("b", out _);

        list.Clear(false);

        AssertInt(list.AsSpan().Length).IsEqual(0);
    }

    [TestCase]
    public void Dispose_MarksDisposed_AndFurtherAddThrows()
    {
        var list = new FreeListCore<string>(new object());

        list.Dispose();

        AssertBool(list.IsDisposed).IsTrue();
        AssertThrown(() => list.Add("a", out _)).IsInstanceOf<ObjectDisposedException>();
    }
}

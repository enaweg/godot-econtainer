using System;
using System.Collections.Generic;
using Enaweg.Container.Godot;
using GdUnit4;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Godot;

[TestSuite]
public class TickableLoopItemTest
{
    sealed class RecordingTickable : ITickable
    {
        public List<long> Frames { get; } = new();
        public void Tick(long frameCount) => Frames.Add(frameCount);
    }

    sealed class ThrowingTickable : ITickable
    {
        public void Tick(long frameCount) => throw new InvalidOperationException("tick failed");
    }

    sealed class RecordingPhysicsTickable : IPhysicsTickable
    {
        public List<long> Frames { get; } = new();
        public void PhysicsTick(long frameCount) => Frames.Add(frameCount);
    }

    [TestCase]
    public void MoveNext_TicksAllEntries()
    {
        var a = new RecordingTickable();
        var b = new RecordingTickable();
        var loopItem = new TickableLoopItem(new ITickable[] { a, b }, null!);

        var result = loopItem.MoveNext(7);

        AssertBool(result).IsTrue();
        AssertArray(a.Frames).ContainsExactly(7L);
        AssertArray(b.Frames).ContainsExactly(7L);
    }

    [TestCase]
    public void MoveNext_AfterDispose_ReturnsFalseAndSkipsTicking()
    {
        var tickable = new RecordingTickable();
        var loopItem = new TickableLoopItem(new ITickable[] { tickable }, null!);

        loopItem.Dispose();
        var result = loopItem.MoveNext(1);

        AssertBool(result).IsFalse();
        AssertArray(tickable.Frames).IsEmpty();
    }

    [TestCase]
    public void MoveNext_ExceptionWithHandler_IsPublishedAndOtherEntriesStillTick()
    {
        Exception? published = null;
        var handler = new EntryPointExceptionHandler(ex => published = ex);
        var after = new RecordingTickable();
        var loopItem = new TickableLoopItem(new ITickable[] { new ThrowingTickable(), after }, handler);

        var result = loopItem.MoveNext(3);

        AssertBool(result).IsTrue();
        AssertObject(published).IsInstanceOf<InvalidOperationException>();
        AssertArray(after.Frames).ContainsExactly(3L);
    }

    [TestCase]
    public void MoveNext_ExceptionWithoutHandler_Rethrows()
    {
        var loopItem = new TickableLoopItem(new ITickable[] { new ThrowingTickable() }, null!);

        AssertThrown(() => loopItem.MoveNext(1)).IsInstanceOf<InvalidOperationException>();
    }

    [TestCase]
    public void FixedTickableLoopItem_MoveNext_TicksAllEntries()
    {
        var a = new RecordingPhysicsTickable();
        var loopItem = new FixedTickableLoopItem(new IPhysicsTickable[] { a }, null!);

        var result = loopItem.MoveNext(9);

        AssertBool(result).IsTrue();
        AssertArray(a.Frames).ContainsExactly(9L);
    }

    [TestCase]
    public void FixedTickableLoopItem_MoveNext_AfterDispose_ReturnsFalse()
    {
        var loopItem = new FixedTickableLoopItem(Array.Empty<IPhysicsTickable>(), null!);

        loopItem.Dispose();

        AssertBool(loopItem.MoveNext(1)).IsFalse();
    }
}

using System;
using Enaweg.Container.Godot;
using GdUnit4;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Godot;

// GodotFrameProvider.Process/.PhysicsProcess are process-wide static singletons and nothing in
// the project ever drives them automatically (FrameProviderDispatcher, the only thing that
// would call .Run() every engine frame, is never instantiated anywhere), so these tests fully
// control when Run() executes. Each test still deregisters what it added (by returning false
// from MoveNext) so leftover work items don't pile up across the suite.
[TestSuite]
[RequireGodotRuntime]
public class GodotFrameProviderTest
{
    sealed class RecordingWorkItem : IFrameRunnerWorkItem
    {
        public int CallCount;
        public bool KeepRunning = true;

        public bool MoveNext(long frameCount)
        {
            CallCount++;
            return KeepRunning;
        }
    }

    sealed class ThrowingWorkItem : IFrameRunnerWorkItem
    {
        public bool MoveNext(long frameCount) => throw new InvalidOperationException("boom");
    }

    [TestCase]
    public void Run_InvokesMoveNextOnRegisteredItem()
    {
        var item = new RecordingWorkItem();
        GodotFrameProvider.Process.Register(item);

        GodotFrameProvider.Process.Run(0.016);

        AssertInt(item.CallCount).IsEqual(1);

        item.KeepRunning = false;
        GodotFrameProvider.Process.Run(0.016);
    }

    [TestCase]
    public void Run_ItemReturningFalse_IsNotCalledAgain()
    {
        var item = new RecordingWorkItem { KeepRunning = false };
        GodotFrameProvider.Process.Register(item);

        GodotFrameProvider.Process.Run(0.016);
        GodotFrameProvider.Process.Run(0.016);

        AssertInt(item.CallCount).IsEqual(1);
    }

    [TestCase]
    public void Run_ItemThatThrows_IsRemovedAndDoesNotStopOtherItems()
    {
        var after = new RecordingWorkItem();
        GodotFrameProvider.Process.Register(new ThrowingWorkItem());
        GodotFrameProvider.Process.Register(after);

        GodotFrameProvider.Process.Run(0.016);
        GodotFrameProvider.Process.Run(0.016);

        // The throwing item is removed after the first Run(); only `after` keeps accumulating.
        AssertInt(after.CallCount).IsEqual(2);

        after.KeepRunning = false;
        GodotFrameProvider.Process.Run(0.016);
    }

    [TestCase]
    public void Run_ItemThatThrows_PublishesToExceptionHandler()
    {
        Exception? published = null;
        GodotFrameProvider.ExceptionHandler = new EntryPointExceptionHandler(ex => published = ex);
        try
        {
            GodotFrameProvider.Process.Register(new ThrowingWorkItem());

            GodotFrameProvider.Process.Run(0.016);

            AssertObject(published).IsInstanceOf<InvalidOperationException>();
        }
        finally
        {
            GodotFrameProvider.ExceptionHandler = null;
        }
    }

    [TestCase]
    public void GetFrameCount_ReturnsNonNegativeValue()
    {
        AssertInt((int)GodotFrameProvider.Process.GetFrameCount()).IsGreaterEqual(0);
        AssertInt((int)GodotFrameProvider.PhysicsProcess.GetFrameCount()).IsGreaterEqual(0);
    }
}

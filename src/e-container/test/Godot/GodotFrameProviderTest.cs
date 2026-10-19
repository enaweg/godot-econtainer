using System;
using System.Collections.Generic;
using Enaweg.Container.Godot;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Godot;

// GodotFrameProvider.Process/.PhysicsProcess are process-wide static singletons that the
// `eContainer` autoload's FrameProviderDispatcher drives every engine frame. A test body runs
// synchronously, so no engine frame lands inside one and the manual Run() calls below are the
// only ticks a test observes - but each test must still deregister what it added (by returning
// false from MoveNext), or the dispatcher keeps ticking it between test cases.
[TestSuite]
[RequireGodotRuntime]
public partial class GodotFrameProviderTest
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

    // Collects what reaches Godot's error stream, so a test can observe GD.PrintErr().
    // Qualified: inside this namespace a bare `Logger` resolves to the Enaweg.Logger namespace.
    sealed partial class ErrorCapturingLogger : global::Godot.Logger
    {
        public readonly List<string> Errors = new List<string>();

        public override void _LogMessage(string message, bool error)
        {
            if (error)
            {
                lock (Errors)
                {
                    Errors.Add(message);
                }
            }
        }
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

    // Without an ExceptionHandler the throwing item used to be removed with nothing logged at
    // all, so a work item could stop for good without a trace.
    [TestCase]
    public void Run_ItemThatThrows_WithoutExceptionHandler_LogsTheException()
    {
        var logger = new ErrorCapturingLogger();
        OS.AddLogger(logger);
        try
        {
            GodotFrameProvider.Process.Register(new ThrowingWorkItem());

            GodotFrameProvider.Process.Run(0.016);

            AssertBool(logger.Errors.Exists(message => message.Contains("boom"))).IsTrue();
        }
        finally
        {
            OS.RemoveLogger(logger);
        }
    }
}

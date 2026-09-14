using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Enaweg.Container.Godot;
using GdUnit4;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Godot;

// FrameTimer never calls GetFrameCount()/Engine APIs from MoveNext, so it can be driven
// entirely by hand without a running Godot engine.
[TestSuite]
public class GodotTimeProviderTest
{
    [TestCase]
    public void MoveNext_FiresCallbackOnceDueTimeElapsed_ThenStops()
    {
        var provider = new GodotFrameProvider(PlayerLoopTiming.Process) { Delta = new StrongBox<double>(0.05) };
        var callCount = 0;
        var timer = new FrameTimer(_ => callCount++, null, TimeSpan.FromSeconds(0.1), Timeout.InfiniteTimeSpan, provider);
        var work = (IFrameRunnerWorkItem)timer;

        AssertBool(work.MoveNext(1)).IsTrue();
        AssertInt(callCount).IsEqual(0);

        AssertBool(work.MoveNext(2)).IsFalse();
        AssertInt(callCount).IsEqual(1);
    }

    [TestCase]
    public void MoveNext_WithPeriod_FiresRepeatedlyAfterDueTime()
    {
        var provider = new GodotFrameProvider(PlayerLoopTiming.Process) { Delta = new StrongBox<double>(0.1) };
        var callCount = 0;
        var timer = new FrameTimer(_ => callCount++, null, TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(0.1), provider);
        var work = (IFrameRunnerWorkItem)timer;

        AssertBool(work.MoveNext(1)).IsTrue();
        AssertInt(callCount).IsEqual(1);

        AssertBool(work.MoveNext(2)).IsTrue();
        AssertInt(callCount).IsEqual(2);

        AssertBool(work.MoveNext(3)).IsTrue();
        AssertInt(callCount).IsEqual(3);
    }

    [TestCase]
    public void Dispose_StopsFurtherTicks()
    {
        var provider = new GodotFrameProvider(PlayerLoopTiming.Process) { Delta = new StrongBox<double>(1.0) };
        var callCount = 0;
        var timer = new FrameTimer(_ => callCount++, null, TimeSpan.FromSeconds(0.1), Timeout.InfiniteTimeSpan, provider);
        var work = (IFrameRunnerWorkItem)timer;

        timer.Dispose();

        AssertBool(work.MoveNext(1)).IsFalse();
        AssertInt(callCount).IsEqual(0);
    }

    [TestCase]
    public void Change_AfterDispose_ReturnsFalse()
    {
        var provider = new GodotFrameProvider(PlayerLoopTiming.Process) { Delta = new StrongBox<double>(0.1) };
        var timer = new FrameTimer(_ => { }, null, TimeSpan.FromSeconds(0.1), Timeout.InfiniteTimeSpan, provider);

        timer.Dispose();
        var changed = timer.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        AssertBool(changed).IsFalse();
    }
}

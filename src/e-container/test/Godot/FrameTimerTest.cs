using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Enaweg.Container.Godot;
using GdUnit4;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Godot;

// FrameTimer never calls GetFrameCount()/Engine APIs from MoveNext, so it can be driven
// entirely by hand without a running Godot engine. GodotTimeProvider itself is covered by
// GodotTimeProviderTest.
[TestSuite]
public class FrameTimerTest
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

    // A 0.25 s period at 0.1 s frames is due at 0.25, 0.5, 0.75 and 1.0 s, so it has to fire on
    // frames 3, 5, 8 and 10. Resetting the elapsed time to zero on each firing dropped the
    // overshoot and fired only on frames 3, 6 and 9 - a period of 0.3 s instead of 0.25 s.
    [TestCase]
    public void MoveNext_WithPeriod_CarriesOvershootIntoTheNextPeriod()
    {
        var provider = new GodotFrameProvider(PlayerLoopTiming.Process) { Delta = new StrongBox<double>(0.1) };
        var firedOnFrames = new List<long>();
        long frame = 0;
        var timer = new FrameTimer(_ => firedOnFrames.Add(frame), null, TimeSpan.FromSeconds(0.25), TimeSpan.FromSeconds(0.25), provider);
        var work = (IFrameRunnerWorkItem)timer;

        for (frame = 1; frame <= 10; frame++)
        {
            work.MoveNext(frame);
        }

        AssertArray(firedOnFrames).ContainsExactly(3L, 5L, 8L, 10L);
    }

    // After a long frame the timer fires once and resumes its schedule, instead of firing on
    // every following frame until it has caught up with each missed period.
    [TestCase]
    public void MoveNext_WithPeriod_DoesNotFireMissedPeriodsBackToBack()
    {
        var delta = new StrongBox<double>(0.1);
        var provider = new GodotFrameProvider(PlayerLoopTiming.Process) { Delta = delta };
        var callCount = 0;
        var timer = new FrameTimer(_ => callCount++, null, TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(0.25), provider);
        var work = (IFrameRunnerWorkItem)timer;

        work.MoveNext(1); // due time reached
        delta.Value = 1.0;
        work.MoveNext(2); // a long frame spanning four periods
        delta.Value = 0.1;
        work.MoveNext(3);

        AssertInt(callCount).IsEqual(2);
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

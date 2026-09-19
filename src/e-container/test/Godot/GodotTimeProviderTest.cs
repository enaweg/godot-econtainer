using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Enaweg.Container.Godot;
using GdUnit4;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Godot;

// GodotTimeProvider.Process/.PhysicsProcess are process-wide singletons whose `time` and
// `Delta` are advanced by the autoload's FrameProviderDispatcher. These tests pin both to known
// values so they assert on exact numbers rather than on whatever the engine has accumulated,
// and restore the originals afterwards.
[TestSuite]
[RequireGodotRuntime]
public class GodotTimeProviderTest
{
    [TestCase]
    public void GetTimestamp_ReflectsAccumulatedFrameTime()
    {
        var original = GodotTimeProvider.Process.time;
        try
        {
            GodotTimeProvider.Process.time = 2.5;

            // gdUnit4 has no long assertion and AssertObject rejects primitives, so compare
            // the timestamp back in seconds.
            AssertFloat(TimeSpan.FromTicks(GodotTimeProvider.Process.GetTimestamp()).TotalSeconds)
                .IsEqualApprox(2.5, 0.0001);
        }
        finally
        {
            GodotTimeProvider.Process.time = original;
        }
    }

    [TestCase]
    public void CreateTimer_ReturnsTimerDrivenByTheProcessFrameProvider()
    {
        var originalDelta = GodotFrameProvider.Process.Delta;
        GodotFrameProvider.Process.Delta = new StrongBox<double>(0.05);
        var fired = 0;
        ITimer? timer = null;
        try
        {
            timer = GodotTimeProvider.Process.CreateTimer(
                _ => fired++,
                null,
                TimeSpan.FromSeconds(0.1),
                Timeout.InfiniteTimeSpan);

            GodotFrameProvider.Process.Run(0.05);
            AssertInt(fired).IsEqual(0);

            GodotFrameProvider.Process.Run(0.05);
            AssertInt(fired).IsEqual(1);
        }
        finally
        {
            timer?.Dispose();
            GodotFrameProvider.Process.Delta = originalDelta;
        }
    }
}

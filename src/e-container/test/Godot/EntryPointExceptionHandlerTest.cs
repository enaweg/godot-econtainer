using System;
using Enaweg.Container.Godot;
using GdUnit4;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Godot;

[TestSuite]
public class EntryPointExceptionHandlerTest
{
    [TestCase]
    public void Publish_InvokesHandlerWithException()
    {
        Exception? received = null;
        var handler = new EntryPointExceptionHandler(ex => received = ex);
        var thrown = new InvalidOperationException("boom");

        handler.Publish(thrown);

        AssertObject(received).IsSame(thrown);
    }

    [TestCase]
    public void Publish_CalledMultipleTimes_InvokesHandlerEachTime()
    {
        var callCount = 0;
        var handler = new EntryPointExceptionHandler(_ => callCount++);

        handler.Publish(new Exception());
        handler.Publish(new Exception());

        AssertInt(callCount).IsEqual(2);
    }
}

using System;
using System.Collections.Generic;
using Enaweg.Container.Internal;
using GdUnit4;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Internal;

[TestSuite]
public class CompositeDisposableTest
{
    sealed class RecordingDisposable(List<string> log, string name) : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
            log.Add(name);
        }
    }

    [TestCase]
    public void Dispose_DisposesAllAddedItemsInLifoOrder()
    {
        var composite = new CompositeDisposable();
        var log = new List<string>();
        var first = new RecordingDisposable(log, "first");
        var second = new RecordingDisposable(log, "second");
        var third = new RecordingDisposable(log, "third");
        composite.Add(first);
        composite.Add(second);
        composite.Add(third);

        composite.Dispose();

        AssertArray(log).ContainsExactly("third", "second", "first");
        AssertBool(first.IsDisposed).IsTrue();
        AssertBool(second.IsDisposed).IsTrue();
        AssertBool(third.IsDisposed).IsTrue();
    }

    [TestCase]
    public void Dispose_CalledTwice_DoesNotReDispose()
    {
        var composite = new CompositeDisposable();
        var log = new List<string>();
        composite.Add(new RecordingDisposable(log, "a"));

        composite.Dispose();
        composite.Dispose();

        AssertArray(log).HasSize(1);
    }

    [TestCase]
    public void Dispose_WithNoItems_DoesNotThrow()
    {
        var composite = new CompositeDisposable();

        composite.Dispose();
    }
}

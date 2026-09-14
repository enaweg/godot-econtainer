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
    public void Dispose_DisposesAllAddedItems()
    {
        var composite = new CompositeDisposable();
        var log = new List<string>();
        var a = new RecordingDisposable(log, "a");
        var b = new RecordingDisposable(log, "b");
        composite.Add(a);
        composite.Add(b);

        composite.Dispose();

        AssertBool(a.IsDisposed).IsTrue();
        AssertBool(b.IsDisposed).IsTrue();
    }

    [TestCase]
    public void Dispose_DisposesInLifoOrder()
    {
        var composite = new CompositeDisposable();
        var log = new List<string>();
        composite.Add(new RecordingDisposable(log, "first"));
        composite.Add(new RecordingDisposable(log, "second"));
        composite.Add(new RecordingDisposable(log, "third"));

        composite.Dispose();

        AssertArray(log).ContainsExactly("third", "second", "first");
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

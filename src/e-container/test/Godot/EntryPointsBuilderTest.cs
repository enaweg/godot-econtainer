using Enaweg.Container.Godot;
using GdUnit4;
using VContainer;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Godot;

[TestSuite]
public class EntryPointsBuilderTest
{
    sealed class RecordingEntryPoint : IInitializable
    {
        public bool Initialized;

        public void Initialize() => Initialized = true;
    }

    [TestCase]
    public void EnsureDispatcherRegistered_RegistersDispatcherOnce()
    {
        var builder = new ContainerBuilder();

        EntryPointsBuilder.EnsureDispatcherRegistered(builder);
        var countAfterFirst = builder.Count;
        EntryPointsBuilder.EnsureDispatcherRegistered(builder);

        AssertBool(builder.Exists(typeof(EntryPointDispatcher), false)).IsTrue();
        AssertInt(builder.Count).IsEqual(countAfterFirst);
    }

    // UseEntryPoints is what constructs EntryPointsBuilder. Without it, Add/OnException were
    // public but unreachable - nothing in the library ever created one.
    [TestCase]
    public void UseEntryPoints_RegistersTheDispatcherAndDispatchesTheAddedEntryPoint()
    {
        var builder = new ContainerBuilder();

        builder.UseEntryPoints(entryPoints => entryPoints.Add<RecordingEntryPoint>(), Lifetime.Singleton);

        AssertBool(builder.Exists(typeof(EntryPointDispatcher), false)).IsTrue();

        using var resolver = builder.Build();

        // Add<T>() registers AsImplementedInterfaces, so the entry point is reachable through
        // IInitializable - resolving the concrete type would build an unrelated instance.
        // The dispatcher runs from a build callback, so Initialize has already happened.
        var entryPoint = (RecordingEntryPoint)resolver.Resolve<IInitializable>();
        AssertBool(entryPoint.Initialized).IsTrue();
    }

    [TestCase]
    public void UseEntryPoints_WithNullConfiguration_Throws()
    {
        var builder = new ContainerBuilder();

        AssertThrown(() => builder.UseEntryPoints(null!))
            .IsInstanceOf<System.ArgumentNullException>();
    }
}

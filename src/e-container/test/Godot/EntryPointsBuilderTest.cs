using Enaweg.Container.Godot;
using GdUnit4;
using VContainer;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Godot;

[TestSuite]
public class EntryPointsBuilderTest
{
    interface IMarkerService
    {
    }

    sealed class MarkerService : IMarkerService, IInitializable
    {
        public void Initialize()
        {
        }
    }

    [TestCase]
    public void EnsureDispatcherRegistered_RegistersDispatcher()
    {
        var builder = new ContainerBuilder();

        EntryPointsBuilder.EnsureDispatcherRegistered(builder);

        AssertBool(builder.Exists(typeof(EntryPointDispatcher), false)).IsTrue();
    }

    [TestCase]
    public void EnsureDispatcherRegistered_IsIdempotent()
    {
        var builder = new ContainerBuilder();

        EntryPointsBuilder.EnsureDispatcherRegistered(builder);
        var countAfterFirst = builder.Count;
        EntryPointsBuilder.EnsureDispatcherRegistered(builder);

        AssertInt(builder.Count).IsEqual(countAfterFirst);
    }

    [TestCase]
    public void Add_RegistersTypeWithImplementedInterfaces()
    {
        var builder = new ContainerBuilder();
        var entryPoints = new EntryPointsBuilder(builder, Lifetime.Singleton);

        entryPoints.Add<MarkerService>();

        AssertBool(builder.Exists(typeof(IMarkerService), true)).IsTrue();
    }

    [TestCase]
    public void OnException_RegistersExceptionHandlerInstance()
    {
        var builder = new ContainerBuilder();
        var entryPoints = new EntryPointsBuilder(builder, Lifetime.Singleton);

        entryPoints.OnException(_ => { });

        AssertBool(builder.Exists(typeof(EntryPointExceptionHandler), false)).IsTrue();
    }
}

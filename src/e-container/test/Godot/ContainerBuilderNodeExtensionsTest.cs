using Enaweg.Container.Godot;
using GdUnit4;
using VContainer;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Godot;

[TestSuite]
public class ContainerBuilderNodeExtensionsTest
{
    interface IEntryPointMarker
    {
    }

    sealed class EntryPointMarker : IEntryPointMarker, IInitializable
    {
        public void Initialize()
        {
        }
    }

    [TestCase]
    public void RegisterEntryPoint_RegistersDispatcherAndInterface()
    {
        var builder = new ContainerBuilder();

        builder.RegisterEntryPoint<EntryPointMarker>();

        AssertBool(builder.Exists(typeof(EntryPointDispatcher), false)).IsTrue();
        AssertBool(builder.Exists(typeof(IEntryPointMarker), true)).IsTrue();
    }

    [TestCase]
    public void RegisterEntryPoint_FuncOverload_RegistersDispatcherAndInterface()
    {
        var builder = new ContainerBuilder();

        builder.RegisterEntryPoint<IEntryPointMarker>(_ => new EntryPointMarker(), Lifetime.Singleton);

        AssertBool(builder.Exists(typeof(EntryPointDispatcher), false)).IsTrue();
        AssertBool(builder.Exists(typeof(IEntryPointMarker), true)).IsTrue();
    }

    [TestCase]
    public void RegisterEntryPointExceptionHandler_RegistersHandlerInstance()
    {
        var builder = new ContainerBuilder();

        builder.RegisterEntryPointExceptionHandler(_ => { });

        using var resolver = builder.Build();

        AssertObject(resolver.ResolveOrDefault<EntryPointExceptionHandler>()).IsNotNull();
    }

    [TestCase]
    public void RegisterNode_ForcesInjectionAtBuildTime_AndIsResolvable()
    {
        var builder = new ContainerBuilder();
        var instance = new object();

        builder.RegisterNode<object>(instance);

        using var resolver = builder.Build();

        AssertObject(resolver.Resolve<object>()).IsSame(instance);
    }
}

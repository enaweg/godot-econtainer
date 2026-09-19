using Enaweg.Container.Godot;
using GdUnit4;
using VContainer;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Godot;

// EntryPointsBuilder's instance API (Add/OnException) is a one-line forward to
// ContainerBuilderNodeExtensions and is not constructed anywhere in the library, so it is
// covered by ContainerBuilderNodeExtensionsTest instead. What is unique here is the
// registration guard in EnsureDispatcherRegistered.
[TestSuite]
public class EntryPointsBuilderTest
{
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
}

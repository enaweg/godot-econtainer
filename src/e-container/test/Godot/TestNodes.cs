using Godot;
using VContainer;

namespace Enaweg.Container.Tests.Godot;

// Shared node doubles. InjectableNode was previously duplicated verbatim in
// LifetimeScopeTest, NodeRegistrationBuilderTest and ObjectResolverNodeExtensionsTest.

/// <summary>A node with a single injectable string dependency, recorded for assertions.</summary>
public sealed partial class InjectableNode : Node
{
    public string? Received;

    [Inject]
    public void Construct(string value) => Received = value;
}

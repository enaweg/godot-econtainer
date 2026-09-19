using Enaweg.Container.Godot;
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

/// <summary>
/// Records whether its injected dependency was already available when _Ready ran.
/// </summary>
public sealed partial class ReadyRecordingNode : Node
{
    public string? Received;
    public string? ReceivedDuringReady;
    public bool ReadyRan;

    [Inject]
    public void Construct(string value) => Received = value;

    public override void _Ready()
    {
        ReadyRan = true;
        ReceivedDuringReady = Received;
    }
}

/// <summary>
/// A scope that can be packed into a <see cref="PackedScene"/> for the
/// <c>CreateChildFromPackedScene</c> tests. Top-level rather than nested so the packed scene
/// can carry its script reference.
/// </summary>
public sealed partial class PackedChildScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance("packed");
    }
}

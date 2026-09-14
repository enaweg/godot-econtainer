using System;
using Enaweg.Container.Godot;
using Godot;
using GdUnit4;
using VContainer;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Godot;

[TestSuite]
[RequireGodotRuntime]
public partial class NodeRegistrationBuilderTest
{
    sealed partial class InjectableNode : Node
    {
        public string? Received;

        [Inject]
        public void Construct(string value) => Received = value;
    }

    [TestCase]
    public void Build_WithExistingNodeInstance_InjectsAndReturnsSameNode()
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance("payload");
        var node = AutoFree(new InjectableNode())!;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(node);
        var registrationBuilder = new NodeRegistrationBuilder(node).As(typeof(InjectableNode));
        builder.Register(registrationBuilder);

        using var resolver = builder.Build();
        var resolved = resolver.Resolve<InjectableNode>();

        AssertObject(resolved).IsSame(node);
        AssertObject(node.Received).IsEqual("payload");
    }

    [TestCase]
    public void Build_WithExistingNodeInstance_IsSingleton()
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance("payload");
        var node = AutoFree(new InjectableNode())!;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(node);
        var registrationBuilder = new NodeRegistrationBuilder(node).As(typeof(InjectableNode));
        builder.Register(registrationBuilder);

        using var resolver = builder.Build();
        var first = resolver.Resolve<InjectableNode>();
        var second = resolver.Resolve<InjectableNode>();

        AssertObject(first).IsSame(second);
    }

    [TestCase]
    public void Build_WithDontDestroyOnLoad_ReparentsAlreadyParentedNodeUnderSceneRoot()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var holder = AutoFree(new Node())!;
        tree.Root.AddChild(holder);
        var node = new InjectableNode();
        holder.AddChild(node);

        var builder = new ContainerBuilder();
        builder.RegisterInstance("payload");
        var registrationBuilder = new NodeRegistrationBuilder(node).DontDestroyOnLoad().As(typeof(InjectableNode));
        builder.Register(registrationBuilder);

        using var resolver = builder.Build();
        resolver.Resolve<InjectableNode>();

        AssertObject(node.GetParent()).IsSame(tree.Root);

        tree.Root.RemoveChild(node);
        node.Free();
    }

    [TestCase]
    public void Build_WithDontDestroyOnLoad_AddsUnparentedNodeUnderSceneRoot()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var node = new InjectableNode();

        var builder = new ContainerBuilder();
        builder.RegisterInstance("payload");
        var registrationBuilder = new NodeRegistrationBuilder(node).DontDestroyOnLoad().As(typeof(InjectableNode));
        builder.Register(registrationBuilder);

        using var resolver = builder.Build();
        resolver.Resolve<InjectableNode>();

        AssertObject(node.GetParent()).IsSame(tree.Root);

        tree.Root.RemoveChild(node);
        node.Free();
    }

    // The scene/prefab/name-based providers are declared but intentionally stubbed out
    // upstream (see NodeRegistrationBuilder.Build()) - these tests pin that current state.
    [TestCase]
    public void Build_FromSceneTreeConstructor_IsNotImplemented()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var registrationBuilder = new NodeRegistrationBuilder(tree, typeof(Node));

        AssertThrown(() => registrationBuilder.Build()).IsInstanceOf<NotImplementedException>();
    }

    [TestCase]
    public void Build_FromPackedSceneFinderConstructor_IsNotImplemented()
    {
        var registrationBuilder = new NodeRegistrationBuilder(_ => new Node(), typeof(Node), Lifetime.Singleton);

        AssertThrown(() => registrationBuilder.Build()).IsInstanceOf<NotImplementedException>();
    }

    [TestCase]
    public void Build_FromGameObjectNameConstructor_IsNotImplemented()
    {
        var registrationBuilder = new NodeRegistrationBuilder("SomeName", typeof(Node), Lifetime.Singleton);

        AssertThrown(() => registrationBuilder.Build()).IsInstanceOf<NotImplementedException>();
    }
}

using Enaweg.Container.Godot;
using Godot;
using GdUnit4;
using VContainer;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Godot;

[TestSuite]
[RequireGodotRuntime]
public partial class ObjectResolverNodeExtensionsTest
{
    [TestCase]
    public void InjectNode_InjectsTargetAndDescendantsRecursively()
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance("payload");
        using var resolver = builder.Build();

        var root = AutoFree(new InjectableNode())!;
        var child = new InjectableNode();
        var grandchild = new InjectableNode();
        root.AddChild(child);
        child.AddChild(grandchild);

        resolver.InjectNode(root);

        AssertObject(root.Received).IsEqual("payload");
        AssertObject(child.Received).IsEqual("payload");
        AssertObject(grandchild.Received).IsEqual("payload");
    }

    // A nested scope resolves its own subtree from its own container, so the parent resolver
    // must stop at that boundary instead of injecting straight through it.
    [TestCase]
    public void InjectNode_StopsAtNestedLifetimeScopes()
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance("payload");
        using var resolver = builder.Build();

        var root = AutoFree(new InjectableNode())!;
        var sibling = new InjectableNode();
        var nestedScope = new LifetimeScope();
        var underNestedScope = new InjectableNode();
        root.AddChild(sibling);
        root.AddChild(nestedScope);
        nestedScope.AddChild(underNestedScope);

        resolver.InjectNode(root);

        AssertObject(root.Received).IsEqual("payload");
        AssertObject(sibling.Received).IsEqual("payload");
        AssertObject(underNestedScope.Received).IsNull();
    }

    [TestCase]
    public void InjectNode_WithNullNode_DoesNotThrow()
    {
        var builder = new ContainerBuilder();
        using var resolver = builder.Build();

        resolver.InjectNode(null!);
    }

    [TestCase]
    public void Instantiate_AddsInstanceUnderParent_AndInjects()
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance("payload");
        using var resolver = builder.Build();

        var template = new InjectableNode();
        var scene = new PackedScene();
        scene.Pack(template);
        template.Free();

        var parent = AutoFree(new Node())!;

        var instance = resolver.Instantiate<InjectableNode>(scene, parent);

        AssertObject(instance.GetParent()).IsSame(parent);
        AssertObject(instance.Received).IsEqual("payload");
    }

    // AddChild() runs _EnterTree/_Ready synchronously, so injecting after it left every
    // injected member null in _Ready - the most likely place to use them.
    [TestCase]
    public void Instantiate_InjectsBeforeTheNodeEntersTheTree()
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance("payload");
        using var resolver = builder.Build();

        var template = new ReadyRecordingNode();
        var scene = new PackedScene();
        scene.Pack(template);
        template.Free();

        // The parent has to be in the tree, otherwise AddChild() never triggers _Ready and the
        // test would pass for the wrong reason.
        var parent = AutoFree(new Node())!;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(parent);

        var instance = resolver.Instantiate<ReadyRecordingNode>(scene, parent);

        AssertBool(instance.ReadyRan).IsTrue();
        AssertObject(instance.ReceivedDuringReady).IsEqual("payload");
    }
}

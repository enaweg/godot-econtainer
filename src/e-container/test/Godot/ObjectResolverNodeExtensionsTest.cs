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
}

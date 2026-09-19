using Enaweg.Container.Godot;
using Godot;
using GdUnit4;
using VContainer;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Godot;

// The project's `eContainer` autoload instantiates a real RootLifetimeScope before any test
// runs, so every scope created here ends up (directly or indirectly) parented under it,
// mirroring how the port is actually used at runtime.
[TestSuite]
[RequireGodotRuntime]
public partial class LifetimeScopeTest
{
    sealed partial class ConfiguringScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance("configured");
        }

        public void SetAutoInject(Node[] nodes) => autoInjectGameObjects = nodes;
        public void SetAutoRun(bool value) => autoRun = value;
    }

    sealed partial class FindParentScope : LifetimeScope
    {
        public LifetimeScope ParentToReturn = null!;

        protected override LifetimeScope FindParent() => ParentToReturn;
    }

    sealed partial class NamedTargetScope : LifetimeScope
    {
    }

    sealed class RecordingInstaller : IInstaller
    {
        public bool Installed { get; private set; }

        public void Install(IContainerBuilder builder)
        {
            Installed = true;
            builder.RegisterInstance(this);
        }
    }

    static RootLifetimeScope Root => (RootLifetimeScope)LifetimeScope.Find<RootLifetimeScope>()!;

    [TestCase]
    public void Build_WithDefaultParent_ResolvesLiveRootAndOwnRegistrations()
    {
        var scope = AutoFree(new ConfiguringScope())!;

        Root.AddChild(scope);

        AssertObject(scope.Parent).IsSame(Root);
        AssertBool(scope.IsRoot).IsFalse();
        AssertObject(scope.Container.Resolve<string>()).IsEqual("configured");
        AssertObject(scope.Container.Resolve<LifetimeScope>()).IsSame(scope);
    }

    [TestCase]
    public void Build_WithAutoRunDisabled_DefersContainerCreationUntilBuild()
    {
        var scope = AutoFree(new ConfiguringScope())!;
        scope.SetAutoRun(false);

        Root.AddChild(scope);

        AssertObject(scope.Container).IsNull();

        scope.Build();

        AssertObject(scope.Container).IsNotNull();
        AssertObject(scope.Container.Resolve<string>()).IsEqual("configured");
    }

    [TestCase]
    public void Create_AddsScopeUnderRoot_AndAppliesConfiguration()
    {
        var scope = AutoFree(LifetimeScope.Create(builder => builder.RegisterInstance("created")))!;

        AssertObject(scope.Parent).IsSame(Root);
        AssertObject(scope.Container.Resolve<string>()).IsEqual("created");
    }

    [TestCase]
    public void CreateChild_WithActionInstaller_ChildResolvesOwnAndParentRegistrations()
    {
        var parent = AutoFree(new ConfiguringScope())!;
        Root.AddChild(parent);

        var child = parent.CreateChild(builder => builder.RegisterInstance(99));

        AssertObject(child.Parent).IsSame(parent);
        AssertInt(child.Container.Resolve<int>()).IsEqual(99);
        AssertObject(child.Container.Resolve<string>()).IsEqual("configured");
    }

    [TestCase]
    public void CreateChild_WithInstaller_InstallsAndIsResolvable()
    {
        var parent = AutoFree(new ConfiguringScope())!;
        Root.AddChild(parent);
        var installer = new RecordingInstaller();

        var child = parent.CreateChild(installer);

        AssertBool(installer.Installed).IsTrue();
        AssertObject(child.Container.Resolve<RecordingInstaller>()).IsSame(installer);
    }

    static PackedScene PackScene(Node root)
    {
        var scene = new PackedScene();
        scene.Pack(root);
        root.Free();
        return scene;
    }

    [TestCase]
    public void CreateChildFromPackedScene_WithScopeAsSceneRoot_AttachesAndInstalls()
    {
        var parent = AutoFree(new ConfiguringScope())!;
        Root.AddChild(parent);
        var scene = PackScene(new PackedChildScope());

        var child = parent.CreateChildFromPackedScene<PackedChildScope>(
            scene,
            builder => builder.RegisterInstance(7));

        AssertObject(child).IsNotNull();
        AssertObject(child.GetParent()).IsSame(parent);
        AssertObject(child.Parent).IsSame(parent);
        AssertObject(child.Container.Resolve<string>()).IsEqual("packed");
        AssertInt(child.Container.Resolve<int>()).IsEqual(7);
    }

    [TestCase]
    public void CreateChildFromPackedScene_WithScopeUnderPlainRoot_AttachesScopeAndDropsWrapper()
    {
        var parent = AutoFree(new ConfiguringScope())!;
        Root.AddChild(parent);

        var wrapper = new Node();
        var scope = new PackedChildScope();
        wrapper.AddChild(scope);
        scope.Owner = wrapper;
        var scene = PackScene(wrapper);

        var child = parent.CreateChildFromPackedScene<PackedChildScope>(scene);

        AssertObject(child).IsNotNull();
        // The plain wrapper root is discarded, not reparented along with the scope.
        AssertObject(child.GetParent()).IsSame(parent);
        AssertObject(child.Parent).IsSame(parent);
        AssertObject(child.Container.Resolve<string>()).IsEqual("packed");
    }

    [TestCase]
    public void CreateChildFromPackedScene_WithoutMatchingScope_ReturnsNullAndAddsNothing()
    {
        var parent = AutoFree(new ConfiguringScope())!;
        Root.AddChild(parent);
        var childCountBefore = parent.GetChildCount();
        var scene = PackScene(new Node());

        var child = parent.CreateChildFromPackedScene<PackedChildScope>(scene);

        AssertObject(child).IsNull();
        AssertInt(parent.GetChildCount()).IsEqual(childCountBefore);
    }

    [TestCase]
    public void Build_UsesFindParentOverride_WhenParentReferenceObjectNotSet()
    {
        var customParent = AutoFree(new ConfiguringScope())!;
        Root.AddChild(customParent);

        var scope = AutoFree(new FindParentScope { ParentToReturn = customParent })!;
        Root.AddChild(scope);

        AssertObject(scope.Parent).IsSame(customParent);
    }

    [TestCase]
    public void Build_WithParentReferenceType_FindsExistingScopeInScene()
    {
        var target = AutoFree(new NamedTargetScope())!;
        Root.AddChild(target);

        var scope = AutoFree(new LifetimeScope())!;
        scope.ParentReference = ParentReference.Create<NamedTargetScope>(typeof(LifetimeScope));
        Root.AddChild(scope);

        AssertObject(scope.Parent).IsSame(target);
    }

    [TestCase]
    public void Find_WithExplicitSceneTree_LocatesScopeUnderRoot()
    {
        var target = AutoFree(new NamedTargetScope())!;
        Root.AddChild(target);

        var found = LifetimeScope.Find<NamedTargetScope>(Root.GetTree());

        AssertObject(found).IsSame(target);
    }

    [TestCase]
    public void Build_InjectsConfiguredAutoInjectGameObjects()
    {
        var scope = AutoFree(new ConfiguringScope())!;
        var injectable = AutoFree(new InjectableNode())!;
        scope.SetAutoInject(new Node[] { injectable });

        Root.AddChild(scope);

        AssertObject(injectable.Received).IsEqual("configured");
    }

    [TestCase]
    public void EnqueueParent_OverridesDefaultParentResolution()
    {
        var customParent = AutoFree(new ConfiguringScope())!;
        Root.AddChild(customParent);

        LifetimeScope scope;
        using (LifetimeScope.EnqueueParent(customParent))
        {
            scope = AutoFree(new LifetimeScope())!;
            Root.AddChild(scope);
        }

        AssertObject(scope.Parent).IsSame(customParent);
    }

    [TestCase]
    public void Enqueue_AppliesGlobalInstallerToNextBuild()
    {
        LifetimeScope scope;
        using (LifetimeScope.Enqueue(b => b.RegisterInstance("global-extra")))
        {
            scope = AutoFree(new LifetimeScope())!;
            Root.AddChild(scope);
        }

        AssertObject(scope.Container.Resolve<string>()).IsEqual("global-extra");
    }

    [TestCase]
    public void Dispose_DisposesContainer()
    {
        var scope = new ConfiguringScope();
        Root.AddChild(scope);
        AssertObject(scope.Container).IsNotNull();

        scope.Dispose();

        AssertObject(scope.Container).IsNull();
    }
}

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
    }

    sealed partial class FindParentScope : LifetimeScope
    {
        public LifetimeScope ParentToReturn = null!;

        protected override LifetimeScope FindParent() => ParentToReturn;
    }

    sealed partial class NamedTargetScope : LifetimeScope
    {
    }

    sealed partial class InjectableNode : Node
    {
        public string? Received;

        [Inject]
        public void Construct(string value) => Received = value;
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
        AssertObject(scope.Container.Resolve<string>()).IsEqual("configured");
        AssertObject(scope.Container.Resolve<LifetimeScope>()).IsSame(scope);
    }

    [TestCase]
    public void IsRoot_IsFalseForNonRootScope()
    {
        var scope = AutoFree(new ConfiguringScope())!;

        Root.AddChild(scope);

        AssertBool(scope.IsRoot).IsFalse();
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

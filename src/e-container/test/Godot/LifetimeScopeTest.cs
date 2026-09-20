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
        public int ConfigureCount { get; private set; }

        protected override void Configure(IContainerBuilder builder)
        {
            ConfigureCount++;
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

    // Deliberately never added to the tree, so looking it up always misses.
    sealed partial class AbsentScope : LifetimeScope
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

    // SceneTree.CurrentScene is null here, as it is while autoloads enter the tree ahead of the
    // main scene and during change_scene_to_*(). Find() used to dereference it unguarded.
    [TestCase]
    public void Find_WithMissingType_ReturnsNullInsteadOfThrowing()
    {
        AssertObject(Root.GetTree().CurrentScene).IsNull();

        AssertObject(LifetimeScope.Find<AbsentScope>()).IsNull();
        AssertObject(LifetimeScope.Find<AbsentScope>(Root.GetTree())).IsNull();
    }

    // The NRE above escaped _EnterTree, which only catches
    // VContainerParentTypeReferenceNotFound, so the scope was silently dropped rather than
    // queued. It should now land on the waiting list.
    [TestCase]
    public void Build_WithParentTypeNotInTreeYet_EnqueuesScopeOnTheWaitingList()
    {
        var waiter = AutoFree(new LifetimeScope())!;
        waiter.ParentReference = ParentReference.Create<AbsentScope>(typeof(LifetimeScope));

        Root.AddChild(waiter);

        AssertBool(RootLifetimeScope.WaitingListContains(waiter)).IsTrue();

        // Leave no dangling entry in the static list for later tests to trip over.
        RootLifetimeScope.CancelReady(waiter);
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

    // EnqueueParent is an explicit, caller-scoped instruction, so it must beat a parent type
    // declared on the scope. It used to be checked only after the declared-type lookup, which
    // meant it was silently ignored for any scope with parentTypeName set.
    [TestCase]
    public void EnqueueParent_OverridesADeclaredParentType()
    {
        var declaredParent = AutoFree(new NamedTargetScope())!;
        Root.AddChild(declaredParent);
        var overrideParent = AutoFree(new ConfiguringScope())!;
        Root.AddChild(overrideParent);

        LifetimeScope scope;
        using (LifetimeScope.EnqueueParent(overrideParent))
        {
            scope = AutoFree(new LifetimeScope())!;
            scope.ParentReference = ParentReference.Create<NamedTargetScope>(typeof(LifetimeScope));
            Root.AddChild(scope);
        }

        AssertObject(scope.Parent).IsSame(overrideParent);
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

    // Dispose(bool) now overrides GodotObject.Dispose(bool) instead of hiding it with `new`, so
    // it also chains to the base implementation. Disposing a scope must still get rid of its
    // node, which base.Dispose() alone does not do for a node that is in the tree.
    [TestCase]
    public void Dispose_QueuesTheNodeForDeletion()
    {
        var scope = new ConfiguringScope();
        Root.AddChild(scope);
        var instanceId = scope.GetInstanceId();

        scope.Dispose();

        // Read through a fresh handle: the scope's own wrapper is disposed by now.
        AssertBool(GodotObject.IsInstanceIdValid(instanceId)).IsTrue();
        var stillLive = (Node)GodotObject.InstanceFromId(instanceId)!;
        AssertBool(stillLive.IsQueuedForDeletion()).IsTrue();
    }

    // Build() is reachable from _EnterTree, from the waiting-list flush and from user code.
    // Without a guard the second call silently orphaned the first container and dispatched the
    // scope's entry points again.
    [TestCase]
    public void Build_CalledAgainAfterBuilding_IsANoOp()
    {
        var scope = AutoFree(new ConfiguringScope())!;
        Root.AddChild(scope);
        var container = scope.Container;
        AssertObject(container).IsNotNull();

        scope.Build();

        AssertObject(scope.Container).IsSame(container);
        AssertInt(scope.ConfigureCount).IsEqual(1);
    }

    // Parent lookup used to check only the direct children of the root scope and of the current
    // scene, so a scope sitting any deeper - the usual layout for one owning a sub-hierarchy -
    // was unreachable and anything declaring it as a parent type queued forever.
    [TestCase]
    public void Find_LocatesAScopeNestedBelowTheFirstLevel()
    {
        var holder = AutoFree(new Node())!;
        Root.AddChild(holder);
        var deeper = AutoFree(new Node())!;
        holder.AddChild(deeper);

        var nested = AutoFree(new NamedTargetScope())!;
        deeper.AddChild(nested);

        AssertObject(LifetimeScope.Find<NamedTargetScope>()).IsSame(nested);
    }

    // Reading parentTypeName used to re-derive it from the resolved Type, which is null whenever
    // the named type did not load - a renamed class, a broken build. The inspector reading the
    // property, or Godot serialising the scene, was then enough to write that loss to disk.
    [TestCase]
    public void ParentTypeName_WithATypeThatDoesNotResolve_SurvivesBeingReadBack()
    {
        var scope = AutoFree(new LifetimeScope())!;

        scope.parentTypeName = "Game.Scopes.DeletedScope";

        AssertString(scope.parentTypeName).IsEqual("Game.Scopes.DeletedScope");
        AssertObject(scope.ParentReference.Type).IsNull();
    }
}

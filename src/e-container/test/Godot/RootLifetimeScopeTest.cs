using Enaweg.Container.Godot;
using Godot;
using GdUnit4;
using VContainer;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Godot;

// The project's `eContainer` autoload instantiates a real RootLifetimeScope before any test
// runs, so these tests exercise the live singleton rather than spinning up a fresh one.
[TestSuite]
[RequireGodotRuntime]
public partial class RootLifetimeScopeTest
{
    sealed partial class OtherTargetScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterInstance("other-target");
        }
    }

    // Never added to the tree, so a scope declaring it as parent stays queued.
    sealed partial class NeverPresentScope : LifetimeScope
    {
    }

    // The two links of a queued chain: MiddleScope waits for OtherTargetScope, LeafScope waits
    // for MiddleScope. Both count their builds so a second one is visible.
    sealed partial class MiddleScope : LifetimeScope
    {
        public int ConfigureCount;

        protected override void Configure(IContainerBuilder builder)
        {
            ConfigureCount++;
            builder.RegisterInstance("middle");
        }
    }

    sealed partial class LeafScope : LifetimeScope
    {
        public int ConfigureCount;

        protected override void Configure(IContainerBuilder builder)
        {
            ConfigureCount++;
        }
    }

    // Stands in for a bootstrap autoload listed after eContainer, or for the main scene: Godot
    // runs its _EnterTree after the root scope's but before any _Ready, which is the window the
    // deferred build exists to keep open.
    sealed partial class LateBootstrapNode : Node
    {
        LifetimeScope.ExtraInstallationScope installation;

        public bool RootWasUnbuiltOnEnterTree;

        public override void _EnterTree()
        {
            RootWasUnbuiltOnEnterTree = LifetimeScope.Find<RootLifetimeScope>()!.Container == null;
            installation = LifetimeScope.Enqueue(b => b.RegisterInstance("late-bootstrap"));
        }

        public override void _ExitTree() => installation.Dispose();
    }

    sealed class DisposableProbe : System.IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    static RootLifetimeScope Root => (RootLifetimeScope)LifetimeScope.Find<RootLifetimeScope>()!;

    [TestCase]
    public void LiveAutoload_IsTheSingletonRoot()
    {
        var root = LifetimeScope.Find<RootLifetimeScope>();

        AssertObject(root).IsNotNull();
        AssertBool(root!.IsRoot).IsTrue();
        AssertObject(root.Container).IsNotNull();
    }

    [TestCase]
    public void SecondInstance_DoesNotReplaceExistingSingleton()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var originalRoot = LifetimeScope.Find<RootLifetimeScope>();
        var second = AutoFree(new RootLifetimeScope())!;

        tree.Root.AddChild(second);
        // RootLifetimeScope's singleton guard throws inside _EnterTree, but Godot's native
        // node-callback bridge logs/swallows exceptions raised from virtual callbacks instead
        // of propagating them back to the AddChild call site - so we assert the guard's
        // effect (the live singleton is untouched) rather than trying to catch an exception.
        var rootAfterAttempt = LifetimeScope.Find<RootLifetimeScope>();

        AssertObject(rootAfterAttempt).IsSame(originalRoot);
        // AddChild() on a node already in the tree runs _EnterTree and _Ready back to back, so
        // this also covers the build now hanging off _Ready: the loser of the singleton race
        // must stay inert in both callbacks, not quietly build a second root container.
        AssertObject(second.Container).IsNull();
    }

    [TestCase]
    public void WaitingList_EnqueueReadyAndCancelReady_TracksMembership()
    {
        var waiter = AutoFree(new LifetimeScope())!;

        AssertBool(RootLifetimeScope.WaitingListContains(waiter)).IsFalse();

        RootLifetimeScope.EnqueueReady(waiter);
        AssertBool(RootLifetimeScope.WaitingListContains(waiter)).IsTrue();

        RootLifetimeScope.CancelReady(waiter);
        AssertBool(RootLifetimeScope.WaitingListContains(waiter)).IsFalse();
    }

    [TestCase]
    public void ReadyWaitingChildren_FlushesOnlyWaitersMatchingAwakenParentType()
    {
        // The parent must be a built scope: waking a waiter now builds it against
        // awakenParent.Container.
        var awakenParent = AutoFree(new OtherTargetScope())!;
        Root.AddChild(awakenParent);

        var matchingWaiter = AutoFree(new LifetimeScope())!;
        matchingWaiter.ParentReference = ParentReference.Create<OtherTargetScope>(typeof(LifetimeScope));
        var otherWaiter = AutoFree(new LifetimeScope())!;
        otherWaiter.ParentReference = ParentReference.Create<NeverPresentScope>(typeof(LifetimeScope));

        RootLifetimeScope.EnqueueReady(matchingWaiter);
        RootLifetimeScope.EnqueueReady(otherWaiter);

        RootLifetimeScope.ReadyWaitingChildren(awakenParent);

        AssertBool(RootLifetimeScope.WaitingListContains(matchingWaiter)).IsFalse();
        AssertObject(matchingWaiter.ParentReference.Object).IsSame(awakenParent);
        AssertBool(RootLifetimeScope.WaitingListContains(otherWaiter)).IsTrue();

        // otherWaiter is about to be freed by AutoFree; it must not be left dangling in the
        // static WaitingList or a later, unrelated ReadyWaitingChildren call would touch a
        // disposed Node.
        RootLifetimeScope.CancelReady(otherWaiter);
    }

    // End to end: a scope whose declared parent is not in the tree yet queues up, and becomes a
    // working scope once that parent arrives. This is the whole point of the waiting list, and
    // nothing asserted it before - the flush only ever called RequestReady(), which re-runs
    // _Ready on tree re-entry and so never built anything.
    [TestCase]
    public void QueuedScope_BuildsOnceItsDeclaredParentEntersTheTree()
    {
        var waiter = AutoFree(new LifetimeScope())!;
        waiter.ParentReference = ParentReference.Create<OtherTargetScope>(typeof(LifetimeScope));
        Root.AddChild(waiter);

        AssertBool(RootLifetimeScope.WaitingListContains(waiter)).IsTrue();
        AssertObject(waiter.Container).IsNull();

        var lateParent = AutoFree(new OtherTargetScope())!;
        Root.AddChild(lateParent);

        AssertBool(RootLifetimeScope.WaitingListContains(waiter)).IsFalse();
        AssertObject(waiter.Parent).IsSame(lateParent);
        AssertObject(waiter.Container).IsNotNull();
        AssertObject(waiter.Container.Resolve<string>()).IsEqual("other-target");
    }

    // The scene-change path: a node entering the tree under the scene root retries the queue.
    // Adding an unrelated node exercises the retry on its own, without the ReadyWaitingChildren
    // call that a parent scope's own Build() would also trigger.
    [TestCase]
    public void NodeEnteringSceneRoot_RetriesQueuedScopes()
    {
        var parent = AutoFree(new OtherTargetScope())!;
        Root.AddChild(parent);

        var waiter = AutoFree(new LifetimeScope())!;
        waiter.ParentReference = ParentReference.Create<OtherTargetScope>(typeof(LifetimeScope));
        RootLifetimeScope.EnqueueReady(waiter);

        var unrelated = AutoFree(new Node())!;
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(unrelated);

        AssertBool(RootLifetimeScope.WaitingListContains(waiter)).IsFalse();
        AssertObject(waiter.Container).IsNotNull();
    }

    // A scope queued behind another queued scope gets built by the parent's own flush, part way
    // through RetryWaitingChildren's iteration over its up-front snapshot. The snapshot still
    // holds it, so the loop used to wake it a second time: a second container, the first one
    // orphaned undisposed, Configure and the entry points run twice, tickables registered twice.
    [TestCase]
    public void RetryWaitingChildren_WithAChainOfQueuedScopes_BuildsEachExactlyOnce()
    {
        // Present and built, so the chain can resolve once the retry reaches it.
        var chainRoot = AutoFree(new OtherTargetScope())!;
        Root.AddChild(chainRoot);

        var middle = AutoFree(new MiddleScope())!;
        middle.ParentReference = ParentReference.Create<OtherTargetScope>(typeof(MiddleScope));
        var leaf = AutoFree(new LeafScope())!;
        leaf.ParentReference = ParentReference.Create<MiddleScope>(typeof(LeafScope));

        // Queued directly, in chain order, without going through the tree: this pins the flush
        // itself rather than the _EnterTree ordering that happens to produce this state.
        RootLifetimeScope.EnqueueReady(middle);
        RootLifetimeScope.EnqueueReady(leaf);

        RootLifetimeScope.RetryWaitingChildren();

        AssertBool(RootLifetimeScope.WaitingListContains(middle)).IsFalse();
        AssertBool(RootLifetimeScope.WaitingListContains(leaf)).IsFalse();
        AssertObject(leaf.Parent).IsSame(middle);
        AssertObject(leaf.Container).IsNotNull();
        AssertObject(leaf.Container.Resolve<string>()).IsEqual("middle");

        AssertInt(middle.ConfigureCount).IsEqual(1);
        AssertInt(leaf.ConfigureCount).IsEqual(1);
    }

    // RootLifetimeScope._ExitTree never chained to base._ExitTree(), so the one place that
    // disposes a scope's container was skipped for the root - every IDisposable singleton
    // registered at root level survived teardown.
    [TestCase]
    public void RootExitingTree_DisposesItsContainer()
    {
        var root = Root;
        var autoloadParent = root.GetParent();
        AssertObject(autoloadParent).IsNotNull();

        try
        {
            // Rebuild the live root with a container-owned disposable in it, so the teardown
            // under test is observable.
            autoloadParent.RemoveChild(root);
            using (LifetimeScope.Enqueue(b => b.Register<DisposableProbe>(Lifetime.Singleton)))
            {
                autoloadParent.AddChild(root);
            }

            var probe = root.Container.Resolve<DisposableProbe>();
            AssertBool(probe.IsDisposed).IsFalse();

            autoloadParent.RemoveChild(root);

            AssertBool(probe.IsDisposed).IsTrue();
            AssertObject(root.Container).IsNull();
        }
        finally
        {
            // Every other test in the run needs a live, built root back.
            if (root.GetParent() == null)
            {
                autoloadParent.AddChild(root);
            }
        }

        AssertObject(LifetimeScope.Find<RootLifetimeScope>()).IsSame(root);
        AssertObject(root.Container).IsNotNull();
    }

    // The root deliberately does not build in _EnterTree. Godot runs every autoload's _EnterTree
    // - and the whole main scene's - before the first _Ready, so building on entry sealed the
    // root container before any other autoload had run a single line, and installers enqueued
    // from there were silently dropped.
    [TestCase]
    public void RootEnteringTree_DefersBuildToReady_SoLaterEnterTreeCodeCanStillEnqueue()
    {
        var root = Root;
        var autoloadParent = root.GetParent();
        var wrapper = AutoFree(new Node())!;
        var bootstrap = new LateBootstrapNode();

        try
        {
            autoloadParent.RemoveChild(root);

            // Both children are attached while wrapper is still detached, so adding wrapper runs
            // root._EnterTree, then bootstrap._EnterTree, and only afterwards the _Ready pass -
            // reproducing the ordering Godot gives a project's autoload list.
            wrapper.AddChild(root);
            wrapper.AddChild(bootstrap);
            autoloadParent.AddChild(wrapper);

            AssertBool(bootstrap.RootWasUnbuiltOnEnterTree).IsTrue();
            AssertObject(root.Container).IsNotNull();
            AssertString(root.Container.Resolve<string>()).IsEqual("late-bootstrap");
        }
        finally
        {
            // Detach the root before wrapper is freed, so the live singleton survives to be
            // restored for every later test in the run.
            if (root.GetParent() == wrapper)
            {
                wrapper.RemoveChild(root);
            }

            if (root.GetParent() == null)
            {
                autoloadParent.AddChild(root);
            }
        }

        AssertObject(LifetimeScope.Find<RootLifetimeScope>()).IsSame(root);
        AssertObject(root.Container).IsNotNull();
    }

    // Godot notifies a node READY once per lifetime unless RequestReady() asks for it again.
    // With the build moved off _EnterTree, a root that leaves and re-enters the tree came back
    // permanently container-less - and every scope parented to it failed to build with it.
    [TestCase]
    public void RootReEnteringTree_BuildsAFreshContainer()
    {
        var root = Root;
        var autoloadParent = root.GetParent();
        var firstContainer = root.Container;
        AssertObject(firstContainer).IsNotNull();

        try
        {
            autoloadParent.RemoveChild(root);
            AssertObject(root.Container).IsNull();

            autoloadParent.AddChild(root);

            AssertObject(root.Container).IsNotNull();
            AssertObject(root.Container).IsNotSame(firstContainer);
        }
        finally
        {
            if (root.GetParent() == null)
            {
                autoloadParent.AddChild(root);
            }
        }
    }

    [TestCase]
    public void RetryWaitingChildren_KeepsScopesWhoseParentIsStillMissing()
    {
        var waiter = AutoFree(new LifetimeScope())!;
        waiter.ParentReference = ParentReference.Create<NeverPresentScope>(typeof(LifetimeScope));
        RootLifetimeScope.EnqueueReady(waiter);

        RootLifetimeScope.RetryWaitingChildren();

        AssertBool(RootLifetimeScope.WaitingListContains(waiter)).IsTrue();
        AssertObject(waiter.Container).IsNull();

        RootLifetimeScope.CancelReady(waiter);
    }
}

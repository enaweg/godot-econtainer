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

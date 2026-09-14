using Enaweg.Container.Godot;
using Godot;
using GdUnit4;
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
    }

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
        var matchingWaiter = AutoFree(new LifetimeScope())!;
        matchingWaiter.ParentReference = ParentReference.Create<OtherTargetScope>(typeof(LifetimeScope));
        var otherWaiter = AutoFree(new LifetimeScope())!;
        otherWaiter.ParentReference = ParentReference.Create<RootLifetimeScope>(typeof(LifetimeScope));
        var awakenParent = AutoFree(new OtherTargetScope())!;

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

    [TestCase]
    public void ReadyWaitingChildren_WithEmptyList_DoesNothing()
    {
        var awakenParent = AutoFree(new OtherTargetScope())!;

        RootLifetimeScope.ReadyWaitingChildren(awakenParent);
    }
}

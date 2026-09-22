using Godot;
using System.Collections.Generic;

namespace Enaweg.Container.Godot;

/// <summary>The singleton top-level <see cref="LifetimeScope"/> installed by the eContainer autoload.</summary>
/// <remarks>
/// It queues scopes whose declared parent has not entered the scene tree yet. Unlike an ordinary scope it builds in
/// <c>_Ready</c> rather than <c>_EnterTree</c>, so that every autoload and the main scene - all of which enter the
/// tree first - can still contribute registrations via <see cref="LifetimeScope.Enqueue(IInstaller)"/>.
/// </remarks>
[GlobalClass]
public sealed partial class RootLifetimeScope : LifetimeScope
{
	private Window treeRoot = null!;
	private static RootLifetimeScope _instance = null!;

	public override void _EnterTree()
	{
		if (_instance != null)
		{
			// Reported rather than thrown: an exception raised from a Godot node callback is
			// logged and swallowed at the native boundary, so it never reaches the AddChild
			// caller and only reads like it is handled. Leaving without claiming the singleton
			// is what actually protects the live root.
			GD.PushError(
				$"A {nameof(RootLifetimeScope)} is already instantiated; this one will stay inert. " +
				"It is installed by the eContainer autoload and should not be created manually.");
			return;
		}

		Root = _instance = this;
		treeRoot = GetTree().Root;

		// Deliberately not built here. Godot runs every autoload's _EnterTree - and the whole
		// main scene's - before the first autoload _Ready, so building now would seal the
		// container before any bootstrap code could LifetimeScope.Enqueue() its installers.
		// The build moves to _Ready, by which point every autoload and the main scene have
		// entered the tree and had a chance to enqueue.
		//
		// AutoRun is suppressed rather than skipping base._EnterTree(), so the base class keeps
		// owning what entering the tree means for a scope; only the build is deferred.
		var autoRun = AutoRun;
		AutoRun = false;
		base._EnterTree();
		AutoRun = autoRun;

		treeRoot.ChildEnteredTree += OnChildEnteredTreeRoot;
	}

	public override void _Ready()
	{
		// The losing instance of a singleton race never claimed Root and has no parent
		// resolved, so it must stay inert here too.
		if (_instance != this)
			return;

		// A no-op when a bootstrapper already forced the build - Build() is idempotent. That
		// happens whenever a scope in the main scene resolves this one as its parent, since the
		// main scene enters the tree before this _Ready runs.
		if (AutoRun)
			Build();
	}

	public override void _ExitTree()
	{
		if (treeRoot != null)
		{
			treeRoot.ChildEnteredTree -= OnChildEnteredTreeRoot;
			treeRoot = null!;
		}

		// base._ExitTree() is what disposes the container. Without it the root scope - and
		// every IDisposable singleton registered in it - survived teardown untouched. It runs
		// before Root is cleared so that anything disposing here still sees a consistent Root.
		base._ExitTree();

		if (_instance == this)
		{
			// The queue belongs to this root's tree; leaving entries behind would keep freed
			// nodes reachable from a static list across a scene reload.
			WaitingList.Clear();

			_instance = null!;
			Root = null!;
		}

		// Godot notifies a node READY only once per lifetime unless it is asked again. Now that
		// the build hangs off _Ready rather than _EnterTree, a root that leaves and re-enters
		// the tree would otherwise come back permanently container-less.
		RequestReady();
	}

	static readonly List<LifetimeScope> WaitingList = new List<LifetimeScope>();

	internal static bool WaitingListContains(LifetimeScope lifetimeScope)
	{
		return WaitingList.Contains(lifetimeScope);
	}

	internal static void EnqueueReady(LifetimeScope lifetimeScope)
	{
		WaitingList.Add(lifetimeScope);
	}

	internal static void CancelReady(LifetimeScope lifetimeScope)
	{
		WaitingList.Remove(lifetimeScope);
	}

	public static void ReadyWaitingChildren(LifetimeScope awakenParent)
	{
		if (WaitingList.Count <= 0) return;

		List<LifetimeScope> buffer = new();
		for (int i = WaitingList.Count - 1; i >= 0; i--)
		{
			LifetimeScope waitingScope = WaitingList[i];
			if (waitingScope.ParentReference.Type != awakenParent.GetType())
				continue;

			waitingScope.ParentReference.Object = awakenParent;
			WaitingList.RemoveAt(i);
			buffer.Add(waitingScope);
		}

		foreach (LifetimeScope waitingScope in buffer)
		{
			Wake(waitingScope);
		}
	}

	/// <summary>
	/// Retries every queued scope, re-queueing the ones whose parent still is not reachable.
	/// </summary>
	internal static void RetryWaitingChildren()
	{
		if (WaitingList.Count <= 0)
			return;

		foreach (LifetimeScope waitingScope in WaitingList.ToArray())
		{
			// Remove first: waking a scope builds it, and Build() re-enters
			// ReadyWaitingChildren, which must not see this scope again.
			//
			// A failed Remove means that nested flush already woke this scope - it is queued
			// behind a parent that is itself queued, and the parent's Build() got to it first.
			// Waking it again here would build it a second time, orphaning the container it
			// just got and dispatching its entry points twice.
			if (!WaitingList.Remove(waitingScope))
				continue;

			Wake(waitingScope);
		}
	}

	private static void Wake(LifetimeScope waitingScope)
	{
		try
		{
			waitingScope.NotifyParentAvailable();
		}
		catch (VContainerParentTypeReferenceNotFound)
		{
			// The parent still is not in the tree - keep waiting for it.
			if (!WaitingList.Contains(waitingScope))
			{
				WaitingList.Add(waitingScope);
			}
		}
		catch (System.Exception ex)
		{
			// One scope failing to configure must not abort the rest of the flush.
			GD.PushError($"Failed to build queued LifetimeScope '{waitingScope.Name}': {ex}");
		}
	}

	private static void OnChildEnteredTreeRoot(Node child)
	{
		// Any node entering the tree under the scene root may be - or may contain - the parent
		// a queued scope is blocked on, the root of a freshly loaded scene most of all. This is
		// a no-op while nothing is queued, so retrying unconditionally costs nothing and does
		// not depend on CurrentScene having been assigned by the time this signal fires.
		RetryWaitingChildren();
	}
}

using Godot;
using System.Collections.Generic;

namespace Enaweg.Container.Godot;

[GlobalClass]
public sealed partial class RootLifetimeScope : LifetimeScope
{
	private Window treeRoot;
	private static RootLifetimeScope _instance;

	public override void _EnterTree()
	{
		if (_instance != null)
		{
			throw new System.InvalidOperationException("RootLiftScope is already instantiated. Do not instantiate it manually.");
		}

		Root = _instance = this;
		treeRoot = GetTree().Root;

		base._EnterTree();
		treeRoot.ChildEnteredTree += OnChildEnteredTreeRoot;
	}

	public override void _ExitTree()
	{
		if (_instance == this)
		{
			_instance = null;
			Root = null;
		}

		if (treeRoot != null)
		{
			treeRoot.ChildEnteredTree -= OnChildEnteredTreeRoot;
			treeRoot = null;
		}
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
			WaitingList.Remove(waitingScope);
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

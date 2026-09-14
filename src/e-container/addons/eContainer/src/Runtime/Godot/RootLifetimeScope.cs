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
			waitingScope.RequestReady();
		}
	}

	private static void OnChildEnteredTreeRoot(Node child)
	{
		// Ignore if child is not in the current scene
		if (child != child.GetTree().CurrentScene)
			return;

		OnSceneChange(child);
	}

	private static void OnSceneChange(Node child)
	{
		if (WaitingList.Count <= 0)
			return;

		List<LifetimeScope> buffer = new();
		for (int i = WaitingList.Count - 1; i >= 0; i--)
		{
			LifetimeScope waitingScope = WaitingList[i];
			if (child.GetTree().CurrentScene != waitingScope.GetTree().CurrentScene)
				continue;

			WaitingList.RemoveAt(i);
			buffer.Add(waitingScope);
		}

		foreach (LifetimeScope waitingScope in buffer)
		{
			waitingScope._Ready(); // Re-throw if parent not found
		}
	}
}

using Godot;
using VContainer;

namespace Enaweg.Container.Godot;

public static class ObjectResolverNodeExtensions
{
	/// <summary>
	/// Injects <paramref name="node"/> and its descendants from this resolver, stopping at any
	/// nested <see cref="LifetimeScope"/>: that scope owns its own subtree and injects it from
	/// its own container. The node passed in is always injected, even if it is itself a scope.
	/// </summary>
	public static void InjectNode(this IObjectResolver resolver, Node node)
	{
		if (node == null) return;

		resolver.Inject(node);
		InjectChildren(resolver, node);
	}

	static void InjectChildren(IObjectResolver resolver, Node current)
	{
		var childCount = current.GetChildCount();
		for (var i = 0; i < childCount; i++)
		{
			Node child = current.GetChild(i);
			if (child is LifetimeScope)
			{
				// Belongs to the nested scope's container, not ours.
				continue;
			}

			resolver.Inject(child);
			InjectChildren(resolver, child);
		}
	}

	public static T Instantiate<T>(this IObjectResolver resolver, PackedScene prefab, Node parent) where T : Node
	{
		var instance = prefab.Instantiate<T>();
		// Inject before the node enters the tree. AddChild() runs _EnterTree and _Ready
		// synchronously, and those callbacks are the most likely place to use an injected
		// member - injecting afterwards leaves them null exactly when they are needed.
		resolver.InjectNode(instance);
		parent.AddChild(instance);
		return instance;
	}
}

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using VContainer;

namespace Enaweg.Container.Godot;

sealed class ExistingNodeProvider : IInstanceProvider
{
	readonly object instance;
	readonly IInjector injector;
	readonly IReadOnlyList<IInjectParameter> customParameters;
	readonly bool isRootObject;

	public ExistingNodeProvider(
		object instance,
		IInjector injector,
		IReadOnlyList<IInjectParameter> customParameters,
		bool isRootObject = false)
	{
		this.instance = instance;
		this.customParameters = customParameters;
		this.injector = injector;
		this.isRootObject = isRootObject;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public object SpawnInstance(IObjectResolver resolver)
	{
		injector.Inject(instance, resolver, customParameters);
		if (isRootObject)
		{
			if (instance is Node node)
			{
				Node root = ((SceneTree)Engine.GetMainLoop()).Root;
				if (node.GetParent() != null)
				{
					// AddChild() fails (and logs an error) for a node that already has a
					// parent; Reparent() is the API meant for moving it instead.
					node.Reparent(root);
				}
				else
				{
					root.AddChild(node);
				}
			}
			else
			{
				throw new VContainerException(instance.GetType(),
					$"Cannot apply `DontDestroyOnLoad`. {instance.GetType().Name} is not a Node");
			}
		}
		return instance;
	}
}

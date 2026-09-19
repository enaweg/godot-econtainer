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
	readonly NodeDestination destination;

	public ExistingNodeProvider(
		object instance,
		IInjector injector,
		IReadOnlyList<IInjectParameter> customParameters,
		NodeDestination destination = default)
	{
		this.instance = instance;
		this.customParameters = customParameters;
		this.injector = injector;
		this.destination = destination;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public object SpawnInstance(IObjectResolver resolver)
	{
		injector.Inject(instance, resolver, customParameters);

		// An explicit UnderTransform() target wins over DontDestroyOnLoad()'s scene root.
		Node target = destination.GetParent(resolver);
		if (target == null && destination.IsRootObject)
		{
			target = ((SceneTree)Engine.GetMainLoop()).Root;
		}

		if (target != null)
		{
			if (instance is not Node node)
			{
				throw new VContainerException(instance.GetType(),
					$"Cannot place {instance.GetType().Name} in the scene tree. It is not a Node");
			}

			if (node.GetParent() != null)
			{
				// AddChild() fails (and logs an error) for a node that already has a
				// parent; Reparent() is the API meant for moving it instead.
				node.Reparent(target);
			}
			else
			{
				target.AddChild(node);
			}
		}

		return instance;
	}
}

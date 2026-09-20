using System;
using Enaweg.Container.Internal;
using Godot;
using VContainer;
using VContainer.Internal;

namespace Enaweg.Container.Godot;

struct NodeDestination
{
	public Node Parent;
	public Func<IObjectResolver, Node> ParentFinder;
	public bool IsRootObject;

	public Node GetParent(IObjectResolver resolver)
	{
		if (Parent != null)
			return Parent;

		if (ParentFinder != null)
			return ParentFinder(resolver);

		return null;
	}
}

public sealed class NodeRegistrationBuilder : RegistrationBuilder
{
	readonly object instance;
	readonly Func<IObjectResolver, Node> packedSceneFinder;
	readonly string gameObjectName;

	NodeDestination destination;
	SceneTree scene;

	internal NodeRegistrationBuilder(object instance)
		: base(GetInstanceType(instance), Lifetime.Singleton)
	{
		this.instance = instance;
	}

	// Named here rather than dereferenced in the base() call, where a null would have surfaced
	// as a bare NullReferenceException from RegisterNode.
	static Type GetInstanceType(object instance)
	{
		ThrowHelper.ThrowArgumentNullIfNull(instance);
		return instance.GetType();
	}

	internal NodeRegistrationBuilder(in SceneTree scene, Type implementationType)
		: base(implementationType, Lifetime.Scoped)
	{
		this.scene = scene;
	}

	internal NodeRegistrationBuilder(Func<IObjectResolver, Node> packedSceneFinder, Type implementationType, Lifetime lifetime)
		: base(implementationType, lifetime)
	{
		this.packedSceneFinder = packedSceneFinder;
	}

	internal NodeRegistrationBuilder(string gameObjectName, Type implementationType, Lifetime lifetime)
		: base(implementationType, lifetime)
	{
		this.gameObjectName = gameObjectName;
	}

	public override Registration Build()
	{
		IInstanceProvider provider;

		if (instance != null)
		{
			var injector = InjectorCache.GetOrBuild(ImplementationType);
			provider = new ExistingNodeProvider(instance, injector, Parameters, destination);
		}
		else if (scene != null)
		{
			// These three stay stubbed deliberately (pinned by NodeRegistrationBuilderTest).
			// The messages name the overload, because the throw lands at container-build time,
			// far from the RegisterNode call that chose this path.
			throw new NotImplementedException(
				$"Registering {ImplementationType} by searching the scene tree is not implemented yet.");
			// provider = new FindNodeProvider(ImplementationType, Parameters, in scene, in destination);
		}
		else if (packedSceneFinder != null)
		{
			throw new NotImplementedException(
				$"Registering {ImplementationType} from a PackedScene is not implemented yet.");
			// var injector = InjectorCache.GetOrBuild(ImplementationType);
			// provider = new PackedSceneNodeProvider(packedSceneFinder, injector, Parameters, in destination);
		}
		else
		{
			throw new NotImplementedException(
				$"Registering {ImplementationType} as a newly created node is not implemented yet.");
			// var injector = InjectorCache.GetOrBuild(ImplementationType);
			// provider = new NewNodeProvider(ImplementationType, injector, Parameters, in destination, gameObjectName);
		}

		return new Registration(ImplementationType, Lifetime, InterfaceTypes, provider);
	}

	public NodeRegistrationBuilder UnderTransform(Node parent)
	{
		destination.Parent = parent;
		return this;
	}

	public NodeRegistrationBuilder UnderTransform(Func<Node> parentFinder)
	{
		destination.ParentFinder = _ => parentFinder();
		return this;
	}

	public NodeRegistrationBuilder UnderTransform(Func<IObjectResolver, Node> parentFinder)
	{
		destination.ParentFinder = parentFinder;
		return this;
	}

	public NodeRegistrationBuilder DontDestroyOnLoad()
	{
		destination.IsRootObject = true;
		return this;
	}
}

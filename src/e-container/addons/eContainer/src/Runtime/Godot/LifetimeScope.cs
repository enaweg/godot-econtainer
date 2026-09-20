using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VContainer;

namespace Enaweg.Container.Godot;


// Node already implements IDisposable - re-declaring it here only served to give the former
// `new Dispose()` the interface slot, diverging from Godot's own disposal path.
public partial class LifetimeScope : Node
{
	public readonly struct ParentOverrideScope : IDisposable
	{
		public ParentOverrideScope(LifetimeScope nextParent)
		{
			lock (SyncRoot)
			{
				GlobalOverrideParents.Push(nextParent);
			}
		}

		public void Dispose()
		{
			lock (SyncRoot)
			{
				GlobalOverrideParents.Pop();
			}
		}
	}

	public readonly struct ExtraInstallationScope : IDisposable
	{
		public ExtraInstallationScope(IInstaller installer)
		{
			lock (SyncRoot)
				GlobalExtraInstallers.Push(installer);
		}

		void IDisposable.Dispose()
		{
			lock (SyncRoot)
				GlobalExtraInstallers.Pop();
		}
	}

	public ParentReference ParentReference;

	[Export]
	public string parentTypeName
	{
		get => ParentReference.TypeName;
		set => ParentReference.TypeName = value;
	}

	[Export] public bool autoRun = true;
	[Export] protected Node[] autoInjectGameObjects = Array.Empty<Node>();
	string scopeName;

	static readonly Stack<LifetimeScope> GlobalOverrideParents = new Stack<LifetimeScope>();
	static readonly Stack<IInstaller> GlobalExtraInstallers = new Stack<IInstaller>();
	static readonly object SyncRoot = new object();

	static LifetimeScope Create(IInstaller installer = null)
	{
		var node = new LifetimeScope();
		node.SetName("LifetimeScope");
		node.localExtraInstallers.Add(installer);
		Root.AddChild(node);
		return node;
	}

	public static LifetimeScope Create(Action<IContainerBuilder> configuration) => Create(new ActionInstaller(configuration));
	public static ParentOverrideScope EnqueueParent(LifetimeScope parent) => new ParentOverrideScope(parent);
	public static ExtraInstallationScope Enqueue(Action<IContainerBuilder> installing) => new ExtraInstallationScope(new ActionInstaller(installing));
	public static ExtraInstallationScope Enqueue(IInstaller installer) => new ExtraInstallationScope(installer);
	public static LifetimeScope Find<T>(SceneTree scene) where T : LifetimeScope => Find(typeof(T), scene);
	public static LifetimeScope Find<T>() where T : LifetimeScope => Find(typeof(T));

	static LifetimeScope Find(Type type, SceneTree scene)
	{
		if (Root == null)
		{
			return null;
		}

		if (type == typeof(RootLifetimeScope))
		{
			return Root;
		}

		if (FindInSubtree(Root, type) is { } scopeUnderRoot)
		{
			return scopeUnderRoot;
		}

		// CurrentScene is null while autoloads enter the tree before the main scene has been
		// instantiated, and again while change_scene_to_*() swaps scenes. There is simply no
		// scene to search then - "not found" is the answer, not a crash.
		Node currentScene = scene?.CurrentScene;
		if (currentScene == null)
		{
			return null;
		}

		if (currentScene is LifetimeScope lifetimeScope && lifetimeScope.GetType() == type)
		{
			return lifetimeScope;
		}

		return FindInSubtree(currentScene, type);
	}

	/// <summary>
	/// Depth-first search of <paramref name="current"/>'s descendants for a scope of exactly
	/// <paramref name="type"/>.
	/// </summary>
	/// <remarks>
	/// Searching the whole subtree, not just direct children: a scope attached partway down a
	/// scene - the usual layout for one that owns a sub-hierarchy - was invisible to parent
	/// lookup before, so anything declaring it as a parent type queued forever.
	/// </remarks>
	static LifetimeScope FindInSubtree(Node current, Type type)
	{
		int childCount = current.GetChildCount(true);
		for (int i = 0; i < childCount; i++)
		{
			Node child = current.GetChild(i, true);

			// `is LifetimeScope` first: ParentReference.Type is resolved from a name stored in
			// the scene, so it is not guaranteed to name a scope type at all.
			if (child is LifetimeScope scope && scope.GetType() == type)
			{
				return scope;
			}

			if (FindInSubtree(child, type) is { } found)
			{
				return found;
			}
		}

		return null;
	}

	static LifetimeScope Find(Type type) => Root == null ? null : Find(type, Root.GetTree());
	protected static RootLifetimeScope Root { get; set; }
	public IObjectResolver Container { get; private set; }
	public LifetimeScope Parent { get; private set; }

	public bool IsRoot => this == Root;

	readonly List<IInstaller> localExtraInstallers = new List<IInstaller>();

	public LifetimeScope() : base()
	{
		ParentReference = new ParentReference()
		{
			OwnerType = GetType()
		};
	}

	public override void _EnterTree()
	{
		try
		{
			Parent = GetRuntimeParent();
			if (autoRun)
			{
				Build();
			}
		}
		catch (VContainerParentTypeReferenceNotFound) when (!IsRoot)
		{
			if (RootLifetimeScope.WaitingListContains(this))
			{
				throw;
			}

			RootLifetimeScope.EnqueueReady(this);
		}
	}

	public override void _ExitTree()
	{
		DisposeCore();
	}

	protected virtual void Configure(IContainerBuilder builder) { }


	// Overrides GodotObject.Dispose(bool) rather than hiding it with `new`. Hiding put this
	// logic in a second, parallel virtual slot: Godot's own disposal path reached the base
	// slot and skipped DisposeCore, while this slot never chained to the base at all - it
	// suppressed the finalizer without ever releasing the native binding.
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			DisposeCore();

			// Unchanged contract: disposing a scope also gets rid of its node. Deferred
			// rather than immediate, because Dispose() may be called from a signal handler
			// or a _Process callback, where freeing a node outright is not safe.
			//
			// Must happen before base.Dispose(), which clears the native pointer this needs.
			if (IsInstanceValid(this) && !IsQueuedForDeletion())
			{
				QueueFree();
			}
		}

		base.Dispose(disposing);
	}

	void DisposeCore()
	{
		Container?.Dispose();
		Container = null;
		RootLifetimeScope.CancelReady(this);
	}

	public void Build()
	{
		// Building twice would silently orphan the first container - its singletons never
		// disposed - and dispatch this scope's entry points a second time, so every tickable
		// would run twice per frame. Build() is reachable from _EnterTree, from the waiting
		// list flush and from user code, so it has to be idempotent.
		if (Container != null)
			return;

		Parent ??= GetRuntimeParent();

		if (Parent != null)
		{
			if (Parent.Container == null)
			{
				// A parent cannot host a child scope before it has a container of its own.
				// This used to be limited to the root scope, which left every other unbuilt
				// parent - an explicit ParentReference.Object, or a parent with autoRun off -
				// to fail with a NullReferenceException below.
				Parent.Build();

				// Parent.Build() flushes the waiting list, which may have built this scope
				// re-entrantly. The guard above already ran, so re-check before continuing.
				if (Container != null)
					return;

				if (Parent.Container == null)
				{
					throw new VContainerException(Parent.GetType(),
						$"{Name} cannot build: its parent scope {Parent.Name} ({Parent.GetType()}) has no container.");
				}
			}

			Parent.Container.CreateScope(builder =>
			{
				builder.RegisterBuildCallback(SetContainer);
				builder.ApplicationOrigin = this;
				builder.Diagnostics = null; // TODO: DiagnosticsContext.GetCollector(scopeName),
				InstallTo(builder);
			});
		}
		else
		{
			var builder = new ContainerBuilder
			{
				ApplicationOrigin = this,
				Diagnostics = null, // TODO: DiagnosticsContext.GetCollector(scopeName),
			};

			builder.RegisterBuildCallback(SetContainer);
			InstallTo(builder);
			builder.Build();
		}

		RootLifetimeScope.ReadyWaitingChildren(this);
	}

	void SetContainer(IObjectResolver container)
	{
		Container = container;
		AutoInjectAll();
	}

	// Called by RootLifetimeScope when a scope this one was queued behind may have become
	// available. Mirrors _EnterTree: resolve the parent, and build only when autoRun is set.
	// Throws VContainerParentTypeReferenceNotFound if the parent still is not reachable.
	internal void NotifyParentAvailable()
	{
		Parent ??= GetRuntimeParent();
		if (autoRun)
		{
			Build();
		}
	}


	public TScope CreateChild<TScope>(IInstaller installer = null) where TScope : LifetimeScope, new()
	{
		var child = new TScope();
		child.SetName("LifetimeScope (Child)");
		if (installer != null)
		{
			child.localExtraInstallers.Add(installer);
		}

		child.ParentReference.Object = this;
		this.AddChild(child);
		return child;
	}

	public LifetimeScope CreateChild(IInstaller installer = null) => CreateChild<LifetimeScope>(installer);

	public TScope CreateChild<TScope>(Action<IContainerBuilder> installation) where TScope : LifetimeScope, new()
		=> CreateChild<TScope>(new ActionInstaller(installation));

	public LifetimeScope CreateChild(Action<IContainerBuilder> installation) => CreateChild<LifetimeScope>(new ActionInstaller(installation));

	public TScope CreateChildFromPackedScene<TScope>(PackedScene scene, IInstaller installer = null) where TScope : LifetimeScope
	{
		Node sceneNode = scene.Instantiate();

		// The scope is normally the scene's root - the direct analogue of VContainer's
		// prefab-with-a-LifetimeScope-component - but scenes that wrap it in a plain root
		// node are tolerated too.
		TScope child = sceneNode as TScope ?? sceneNode.GetChildren().OfType<TScope>().FirstOrDefault();
		if (child == null)
		{
			GD.PushWarning($"PackedScene {scene.ResourcePath} does not contain a {typeof(TScope).Name}.");
			sceneNode.Free();
			return null;
		}

		if (installer != null)
		{
			child.localExtraInstallers.Add(installer);
		}

		// AddChild() fails on a node that still has a parent, so detach the scope from the
		// instantiated scene first and free the wrapper that is left behind.
		if (child != sceneNode)
		{
			sceneNode.RemoveChild(child);
			sceneNode.Free();
		}

		// Must be assigned before the node enters the tree: _EnterTree() resolves the parent
		// scope and builds the container.
		child.ParentReference.Object = this;
		AddChild(child);
		return child;
	}

	public TScope CreateChildFromPackedScene<TScope>(PackedScene scene, Action<IContainerBuilder> installation) where TScope : LifetimeScope
		=> CreateChildFromPackedScene<TScope>(scene, new ActionInstaller(installation));

	void InstallTo(IContainerBuilder builder)
	{
		Configure(builder);

		foreach (IInstaller installer in localExtraInstallers)
		{
			installer.Install(builder);
		}

		localExtraInstallers.Clear();

		lock (SyncRoot)
		{
			foreach (IInstaller installer in GlobalExtraInstallers)
			{
				installer.Install(builder);
			}
		}

		builder.RegisterInstance(this).AsSelf();
		EntryPointsBuilder.EnsureDispatcherRegistered(builder);
	}

	protected virtual LifetimeScope FindParent() => null;

	LifetimeScope GetRuntimeParent()
	{
		if (IsRoot) return null;

		if (ParentReference.Object != null)
			return ParentReference.Object;

		// Find via implementation
		LifetimeScope implParent = FindParent();
		if (implParent != null)
		{
			if (ParentReference.Type != null && ParentReference.Type != implParent.GetType())
			{
				GD.PushWarning($"FindParent returned {implParent.GetType()} but parent parentReference type is {ParentReference.Type}. This may be unintentional.");
			}
			return implParent;
		}

		// An EnqueueParent() override is an explicit, caller-scoped instruction, so it wins
		// over a parent type declared in the scene. Checking it after the type lookup below
		// meant the override was silently ignored for any scope with parentTypeName set.
		lock (SyncRoot)
		{
			if (GlobalOverrideParents.Count > 0)
			{
				return GlobalOverrideParents.Peek();
			}
		}

		// Normalise the declared type before looking it up: an unset parent type, or one
		// naming this scope's own type, both mean "parent to the root scope". This used to be
		// written as two identical copies of the lookup below, one on either side of the
		// normalisation.
		if (ParentReference.Type == GetType())
		{
			GD.PushWarning("Parent reference cannot be same as self.");
		}

		if (ParentReference.Type == null || ParentReference.Type == GetType())
		{
			ParentReference = ParentReference.Create<RootLifetimeScope>(GetType());
		}

		// Only a stray RootLifetimeScope - one that lost the singleton race and so is not Root -
		// normalises to its own type. It has no parent to find.
		if (ParentReference.Type == GetType())
		{
			return null;
		}

		// Find in scene via type
		if (Find(ParentReference.Type) is { Container: not null } foundScope)
			return foundScope;

		throw new VContainerParentTypeReferenceNotFound(ParentReference.Type, $"{Name} could not found parent reference of type : {ParentReference.Type}");
	}

	void AutoInjectAll()
	{
		if (autoInjectGameObjects == null)
			return;

		foreach (Node target in autoInjectGameObjects)
		{
			if (target != null) // Check missing reference
			{
				Container.InjectNode(target);
			}
		}
	}
}

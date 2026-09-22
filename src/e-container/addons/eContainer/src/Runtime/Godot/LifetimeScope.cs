using System;
using System.Collections.Generic;
using System.Linq;
using Enaweg.Container.Internal;
using Godot;
using VContainer;

namespace Enaweg.Container.Godot;


// Node already implements IDisposable - re-declaring it here only served to give the former
// `new Dispose()` the interface slot, diverging from Godot's own disposal path.
//
// [GlobalClass] so a plain LifetimeScope can be picked by name in the editor's Create Node
// dialog, instead of having to be created as a Node with the script attached by hand.
/// <summary>A Godot node that owns a VContainer scope for itself and its child scopes.</summary>
/// <remarks>
/// With <see cref="AutoRun"/> enabled, the container builds in <c>_EnterTree</c> and is disposed in
/// <c>_ExitTree</c>. A scope inherits from its resolved <see cref="Parent"/>; otherwise it creates a root container.
/// <see cref="RootLifetimeScope"/> is the one exception: it defers its build to <c>_Ready</c>, so scopes entering
/// the tree before it has built queue up and are flushed once it does.
/// </remarks>
[GlobalClass]
public partial class LifetimeScope : Node
{
	/// <summary>Temporarily supplies the parent used by scopes created within the surrounding <c>using</c> block.</summary>
	public readonly struct ParentOverrideScope : IDisposable
	{
		readonly LifetimeScope pushed;

		public ParentOverrideScope(LifetimeScope nextParent)
		{
			pushed = nextParent;
			lock (SyncRoot)
			{
				GlobalOverrideParents.Add(nextParent);
			}
		}

		public void Dispose()
		{
			lock (SyncRoot)
			{
				RemoveLast(GlobalOverrideParents, pushed);
			}
		}
	}

	/// <summary>Temporarily adds an installer to scopes created within the surrounding <c>using</c> block.</summary>
	public readonly struct ExtraInstallationScope : IDisposable
	{
		readonly IInstaller pushed;

		public ExtraInstallationScope(IInstaller installer)
		{
			pushed = installer;
			lock (SyncRoot)
				GlobalExtraInstallers.Add(installer);
		}

		// Public, matching ParentOverrideScope. As an explicit interface implementation `using`
		// had to box the struct to reach it.
		public void Dispose()
		{
			lock (SyncRoot)
				RemoveLast(GlobalExtraInstallers, pushed);
		}
	}

	/// <summary>Serialized and runtime information used to locate this scope's parent.</summary>
	public ParentReference ParentReference = default;

	/// <summary>Gets or sets the serialized parent-scope type name shown by Godot's inspector.</summary>
	[Export]
	public string ParentTypeName
	{
		get => ParentReference.TypeName;
		set => ParentReference.TypeName = value;
	}

	/// <summary>Gets or sets whether this scope builds automatically when it enters the scene tree.</summary>
	[Export] public bool AutoRun = true;

	/// <summary>Nodes injected from this scope's container as soon as it is built.</summary>
	[Export] protected Node[] AutoInjectNodes = Array.Empty<Node>();

	// Used as stacks, but list-backed so disposal can remove the entry that scope actually
	// pushed instead of whatever happens to be on top. Stack.Pop() threw on an empty stack
	// after a double dispose, and popped someone else's entry when two overlapping scopes were
	// disposed out of order - both corrupt state shared by every scope being built.
	static readonly List<LifetimeScope> GlobalOverrideParents = new List<LifetimeScope>();
	static readonly List<IInstaller> GlobalExtraInstallers = new List<IInstaller>();
	static readonly object SyncRoot = new object();

	/// <summary>
	/// Removes the topmost entry identical to <paramref name="item"/>, or nothing if it is no
	/// longer there - a second Dispose() on the same scope is a no-op rather than a corruption.
	/// </summary>
	static void RemoveLast<T>(List<T> stack, T item) where T : class
	{
		for (int i = stack.Count - 1; i >= 0; i--)
		{
			if (ReferenceEquals(stack[i], item))
			{
				stack.RemoveAt(i);
				return;
			}
		}
	}

	static LifetimeScope Create(IInstaller installer)
	{
		if (Root == null)
		{
			throw new InvalidOperationException(
				$"Cannot create a {nameof(LifetimeScope)} before the eContainer autoload has entered the tree.");
		}

		var node = new LifetimeScope();
		node.SetName("LifetimeScope");
		// A null installer would only surface later, as a NullReferenceException inside
		// InstallTo(); the no-installer case is just an empty list.
		if (installer != null)
		{
			node.localExtraInstallers.Add(installer);
		}

		Root.AddChild(node);
		return node;
	}

	/// <summary>Creates and attaches a child of the eContainer root scope using <paramref name="configuration"/>.</summary>
	/// <exception cref="InvalidOperationException">The eContainer root autoload has not entered the tree.</exception>
	public static LifetimeScope Create(Action<IContainerBuilder> configuration) => Create(new ActionInstaller(configuration));
	/// <summary>Temporarily overrides the parent used for scopes created in the returned scope's lifetime.</summary>
	public static ParentOverrideScope EnqueueParent(LifetimeScope parent) => new ParentOverrideScope(parent);
	/// <summary>Temporarily adds registrations to scopes created in the returned scope's lifetime.</summary>
	public static ExtraInstallationScope Enqueue(Action<IContainerBuilder> installing) => new ExtraInstallationScope(new ActionInstaller(installing));
	/// <summary>Temporarily adds <paramref name="installer"/> to scopes created in the returned scope's lifetime.</summary>
	public static ExtraInstallationScope Enqueue(IInstaller installer) => new ExtraInstallationScope(installer);
	/// <summary>Finds the active scope of type <typeparamref name="T"/> in <paramref name="scene"/>.</summary>
	public static LifetimeScope Find<T>(SceneTree scene) where T : LifetimeScope => Find(typeof(T), scene);
	/// <summary>Finds the active scope of type <typeparamref name="T"/> in the current scene.</summary>
	public static LifetimeScope Find<T>() where T : LifetimeScope => Find(typeof(T));

	static LifetimeScope Find(Type type, SceneTree scene)
	{
		if (Root == null)
		{
			return null!;
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
		Node currentScene = scene?.CurrentScene!;
		if (currentScene == null)
		{
			return null!;
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

		return null!;
	}

	static LifetimeScope Find(Type type) => Root == null ? null! : Find(type, Root.GetTree());
	protected static RootLifetimeScope Root { get; set; } = null!;
	/// <summary>Gets this scope's built resolver, or <see langword="null"/> until it has built or after disposal.</summary>
	public IObjectResolver Container { get; private set; } = null!;
	/// <summary>Gets the resolved parent scope, or <see langword="null"/> when this scope is a root scope.</summary>
	public LifetimeScope Parent { get; private set; } = null!;

	/// <summary>Gets whether this instance is the active eContainer root scope.</summary>
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
			if (AutoRun)
			{
				Build();
			}
		}
		catch (VContainerParentTypeReferenceNotFound) when (!IsRoot)
		{
			// Queue up and wait for the declared parent to enter the tree. EnqueueReady is
			// idempotent-safe here: _ExitTree dequeues via DisposeCore, so a scope cannot
			// re-enter the tree while still listed. This used to rethrow in that case, which
			// only threw across Godot's native callback boundary - where it is logged and
			// swallowed, never reaching the AddChild caller.
			if (!RootLifetimeScope.WaitingListContains(this))
			{
				RootLifetimeScope.EnqueueReady(this);
			}
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
		Container = null!;
		// Cleared too, so a torn-down scope stops keeping its parent node reachable. _EnterTree
		// resolves it again from scratch if this scope re-enters the tree.
		Parent = null!;
		RootLifetimeScope.CancelReady(this);
	}

	/// <summary>Builds this scope's resolver and dispatches its registered entry points.</summary>
	/// <remarks>This method is idempotent. It builds an unbuilt parent first when required.</remarks>
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
				// parent - an explicit ParentReference.Object, or a parent with AutoRun off -
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
				builder.Diagnostics = null; // TODO: DiagnosticsContext.GetCollector(Name),
				InstallTo(builder);
			});
		}
		else
		{
			var builder = new ContainerBuilder
			{
				ApplicationOrigin = this,
				Diagnostics = null, // TODO: DiagnosticsContext.GetCollector(Name),
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
	// available. Mirrors _EnterTree: resolve the parent, and build only when AutoRun is set.
	// Throws VContainerParentTypeReferenceNotFound if the parent still is not reachable.
	internal void NotifyParentAvailable()
	{
		Parent ??= GetRuntimeParent();
		if (AutoRun)
		{
			Build();
		}
	}


	/// <summary>Creates, attaches, and builds a child scope of type <typeparamref name="TScope"/>.</summary>
	/// <param name="installer">Optional registrations applied only to the child scope.</param>
	public TScope CreateChild<TScope>(IInstaller? installer = null) where TScope : LifetimeScope, new()
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

	/// <summary>Creates, attaches, and builds a plain child <see cref="LifetimeScope"/>.</summary>
	public LifetimeScope CreateChild(IInstaller? installer = null) => CreateChild<LifetimeScope>(installer);

	/// <summary>Creates a child scope configured by <paramref name="installation"/>.</summary>
	public TScope CreateChild<TScope>(Action<IContainerBuilder> installation) where TScope : LifetimeScope, new()
		=> CreateChild<TScope>(new ActionInstaller(installation));

	/// <summary>Creates a plain child scope configured by <paramref name="installation"/>.</summary>
	public LifetimeScope CreateChild(Action<IContainerBuilder> installation) => CreateChild<LifetimeScope>(new ActionInstaller(installation));

	/// <summary>Instantiates a packed scene, finds its child scope, and attaches that scope to this scope.</summary>
	/// <returns>The discovered scope, or <see langword="null"/> when the scene contains none of the requested type.</returns>
	public TScope CreateChildFromPackedScene<TScope>(PackedScene scene, IInstaller? installer = null) where TScope : LifetimeScope
	{
		ThrowHelper.ThrowArgumentNullIfNull(scene);

		Node sceneNode = scene.Instantiate();

		// The scope is normally the scene's root - the direct analogue of VContainer's
		// prefab-with-a-LifetimeScope-component - but scenes that wrap it in a plain root
		// node are tolerated too.
		TScope child = (sceneNode as TScope ?? sceneNode.GetChildren().OfType<TScope>().FirstOrDefault())!;
		if (child == null)
		{
			GD.PushWarning($"PackedScene {scene.ResourcePath} does not contain a {typeof(TScope).Name}.");
			sceneNode.Free();
			return null!;
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

	/// <summary>Instantiates a packed-scene child scope configured by <paramref name="installation"/>.</summary>
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
			// Back to front: these are installed most-recently-enqueued first, which is the
			// order the Stack this list replaced iterated in.
			for (int i = GlobalExtraInstallers.Count - 1; i >= 0; i--)
			{
				GlobalExtraInstallers[i].Install(builder);
			}
		}

		builder.RegisterInstance(this).AsSelf();
		EntryPointsBuilder.EnsureDispatcherRegistered(builder);
	}

	protected virtual LifetimeScope FindParent() => null!;

	LifetimeScope GetRuntimeParent()
	{
		if (IsRoot) return null!;

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
		// meant the override was silently ignored for any scope with ParentTypeName set.
		lock (SyncRoot)
		{
			if (GlobalOverrideParents.Count > 0)
			{
				return GlobalOverrideParents[^1];
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
			return null!;
		}

		// Find in scene via type
		if (Find(ParentReference.Type) is { Container: not null } foundScope)
			return foundScope;

		throw new VContainerParentTypeReferenceNotFound(ParentReference.Type, $"{Name} could not found parent reference of type : {ParentReference.Type}");
	}

	void AutoInjectAll()
	{
		if (AutoInjectNodes == null)
			return;

		foreach (Node target in AutoInjectNodes)
		{
			if (target != null) // Check missing reference
			{
				Container.InjectNode(target);
			}
		}
	}
}

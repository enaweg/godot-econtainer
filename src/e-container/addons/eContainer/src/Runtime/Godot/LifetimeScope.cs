using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;
using VContainer;
using Array = System.Array;

namespace Enaweg.Container.Godot;


public partial class LifetimeScope : Node, IDisposable
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
		if (type == typeof(RootLifetimeScope))
		{
			return Root;
		}

		Array<Node> rootChildren = Root.GetChildren(true);
		foreach (Node child in rootChildren)
		{
			if (child.GetType() == type)
			{
				return child as LifetimeScope;
			}
		}

		Array<Node> childArray = scene.CurrentScene.GetChildren();
		if (scene.CurrentScene is LifetimeScope lifetimeScope && lifetimeScope.GetType() == type)
		{
			return lifetimeScope;
		}

		foreach (Node child in childArray)
		{
			if (child.GetType() == type)
			{
				return child as LifetimeScope;
			}
		}

		return null;
	}

	static LifetimeScope Find(Type type) => Find(type, Root.GetTree());
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


	public new void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected new virtual void Dispose(bool disposing)
	{
		if (!disposing)
			return;

		DisposeCore();
		QueueFree();
	}

	void DisposeCore()
	{
		Container?.Dispose();
		Container = null;
		RootLifetimeScope.CancelReady(this);
	}

	public void Build()
	{
		Parent ??= GetRuntimeParent();

		if (Parent != null)
		{
			if (Parent.IsRoot)
			{
				if (Parent.Container == null)
					Parent.Build();
			}

			// ReSharper disable once PossibleNullReferenceException
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
		var child = sceneNode.GetChildren().FirstOrDefault() as TScope;
		if (child == null)
		{
			GD.PushWarning($"PackedScene {scene.ResourcePath} does not contain a {typeof(TScope).Name}.");
			return null;
		}

		if (installer != null)
		{
			child.localExtraInstallers.Add(installer);
		}

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

		// Find in scene via type
		if (ParentReference.Type != null && ParentReference.Type != GetType())
		{
			if (Find(ParentReference.Type) is { Container: not null } foundScope)
				return foundScope;

			throw new VContainerParentTypeReferenceNotFound(ParentReference.Type, $"{Name} could not found parent reference of type : {ParentReference.Type}");
		}

		lock (SyncRoot)
		{
			if (GlobalOverrideParents.Count > 0)
			{
				return GlobalOverrideParents.Peek();
			}
		}

		if (ParentReference.Type == null)
		{
			ParentReference = ParentReference.Create<RootLifetimeScope>(GetType());
		}

		if (ParentReference.Type == GetType())
		{
			GD.PushWarning("Parent reference cannot be same as self.");
			ParentReference = ParentReference.Create<RootLifetimeScope>(GetType());
		}
		
		if (ParentReference.Type != null && ParentReference.Type != GetType())
		{
			if (Find(ParentReference.Type) is { Container: not null } foundScope)
				return foundScope;

			throw new VContainerParentTypeReferenceNotFound(ParentReference.Type, $"{Name} could not found parent reference of type : {ParentReference.Type}");
		}
		return null;
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

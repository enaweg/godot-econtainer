using System;

namespace Enaweg.Container.Godot;

/// <summary>Serialized and runtime information identifying a <see cref="LifetimeScope"/>'s parent.</summary>
/// <remarks>The inspector persists <see cref="TypeName"/>. <see cref="Object"/> takes precedence when a parent has been assigned directly.</remarks>
public partial struct ParentReference
{
	private string typeName;

	/// <summary>
	/// The assembly-qualified-ish name of the parent scope type, as stored in the scene.
	/// </summary>
	/// <remarks>
	/// The getter is a plain field read. It used to re-derive the name from <see cref="Type"/>
	/// first, which silently erased the stored name whenever the type had failed to resolve -
	/// a renamed class, an assembly not loaded yet, a broken build. The inspector reading the
	/// property, or Godot serialising the scene, was enough to write that loss to disk.
	/// </remarks>
	public string TypeName
	{
		get => typeName;
		set
		{
			typeName = value;
			OnAfterDeserialize();
		}
	}

	/// <summary>Gets or sets the explicitly assigned parent scope, if any.</summary>
	public LifetimeScope Object;

	/// <summary>Gets the scope type that owns this reference.</summary>
	public Type OwnerType { get; init; }
	/// <summary>Gets the parent type resolved from <see cref="TypeName"/>, or <see langword="null"/> when it cannot be resolved.</summary>
	public Type Type { get; private set; }
	
	ParentReference(Type ownerType, Type type) : this()
	{
		Type = type;
		typeName = type.FullName;
		Object = null;
		OwnerType = ownerType;
	}

	/// <summary>Resolves <see cref="TypeName"/> against currently loaded assemblies.</summary>
	/// <remarks>An unresolved name is retained so the serialized reference can be repaired later.</remarks>
	public void OnAfterDeserialize()
	{
		if (string.IsNullOrEmpty(typeName))
		{
			Type = null;
			return;
		}

		Type resolved = null;
		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			resolved = assembly.GetType(typeName);
			if (resolved != null)
				break;
		}

		Type = resolved;
	}

	/// <summary>Creates a reference to a parent scope of type <typeparamref name="T"/>.</summary>
	public static ParentReference Create<T>(Type ownerType) => new ParentReference(ownerType, typeof(T));
}

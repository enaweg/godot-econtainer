using System;
using System.ComponentModel.DataAnnotations;
using Godot;

namespace Enaweg.Container.Godot;

public partial struct ParentReference
{
	private string typeName;

	[Export]
	public string TypeName
	{
		get
		{
			OnBeforeSerialize();
			return typeName;
		}
		set
		{
			typeName = value;
			OnAfterDeserialize();
		}
	}

	public LifetimeScope Object;

	[Required] public Type OwnerType { get; init; }
	public Type Type { get; private set; }
	
	ParentReference(Type type) : this()
	{
		Type = type;
		typeName = type.FullName;
		Object = null;
	}
	
	
	ParentReference(Type ownerType, Type type) : this()
	{
		Type = type;
		typeName = type.FullName;
		Object = null;
		OwnerType = ownerType;
	}

	private void OnBeforeSerialize()
	{
		this.typeName = Type?.FullName;
	}

	public void OnAfterDeserialize()
	{
		if (!string.IsNullOrEmpty(typeName))
		{
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type = assembly.GetType(typeName);
				if (Type != null)
					break;
			}
		}
		else
		{
			Type = null;
		}
	}

	public static ParentReference Create<T>(Type ownerType) => new ParentReference(ownerType, typeof(T));
}

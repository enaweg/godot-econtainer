using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Enaweg.Container.Godot;

public static class TypeCache
{
	private static readonly Dictionary<RuntimeTypeHandle, List<Type>> cache = new Dictionary<RuntimeTypeHandle, List<Type>>();

	/// <summary>
	/// Returns every concrete type assignable to <typeparamref name="T"/>, including interface
	/// implementations, excluding <typeparamref name="T"/> itself and abstract types.
	/// </summary>
	/// <remarks>
	/// Results are cached for the lifetime of the assembly. A C# assembly reload in the editor
	/// wipes static state, so newly written types are picked up on the next reload.
	/// </remarks>
	public static List<Type> GetTypesDerivedFrom<T>()
	{
		var baseType = typeof(T);

		if (cache.TryGetValue(baseType.TypeHandle, out var cachedTypeList))
		{
			return cachedTypeList;
		}

		var derivedTypeList = AppDomain.CurrentDomain.GetAssemblies()
			.SelectMany(GetLoadableTypes)
			.Where(type => baseType.IsAssignableFrom(type) && type != baseType && !type.IsAbstract)
			.ToList();

		cache[baseType.TypeHandle] = derivedTypeList;

		return derivedTypeList;
	}

	/// <summary>
	/// Assembly.GetTypes() throws if any type in the assembly cannot be loaded - a single
	/// optional dependency missing anywhere in the process would otherwise take out every
	/// caller, including the LifetimeScope inspector. Keep whatever did load.
	/// </summary>
	private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			return ex.Types.Where(type => type != null)!;
		}
		catch (Exception)
		{
			return Array.Empty<Type>();
		}
	}
}

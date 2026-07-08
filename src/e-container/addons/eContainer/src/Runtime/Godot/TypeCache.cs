using System;
using System.Collections.Generic;
using System.Linq;

namespace Enaweg.Container.Godot;

public static class TypeCache
{
	private static readonly Dictionary<RuntimeTypeHandle, List<Type>> cache = new Dictionary<RuntimeTypeHandle, List<Type>>();

	/// <summary>
	/// 獲取所有繼承自 T 的類型（包括接口實現）。
	/// </summary>
	public static List<Type> GetTypesDerivedFrom<T>()
	{
		var baseType = typeof(T);

		// 如果已經緩存，直接返回
		if (cache.TryGetValue(baseType.TypeHandle, out var cachedTypeList))
		{
			return cachedTypeList;
		}

		// 搜索所有加載的程序集
		var derivedTypeList = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes())
			.Where(type => baseType.IsAssignableFrom(type) && type != baseType && !type.IsAbstract)
			.ToList();

		// 緩存結果
		cache[baseType.TypeHandle] = derivedTypeList;

		return derivedTypeList;
	}
}

#if TOOLS
using System.Linq;
using System.Runtime.CompilerServices;
using Enaweg.Plugin;
using Godot;

namespace Enaweg.Container.Editor;

/// <summary>Registers the <see cref="LifeScopeInspectorPlugin"/> with the eContainer editor plugin once ePlugin has started.</summary>
internal sealed class LifeScopeInspectorInitializer : IInitialize
{
	// Kept on the plugin node rather than in a field: the node and the inspector registered with
	// it both outlive an assembly reload, after which this initializer runs again.
	const string InspectorMeta = "_lifetime_scope_inspector";

	// Runs on every assembly load, so the initializer is queued again after each reload. ePlugin
	// runs queued initializers whenever it starts up, which an assembly reload includes; outside
	// the editor it never starts, and the queued initializer is simply never run.
	//
	// Must not call into the engine: the assembly is also loaded by plain .NET test hosts, where
	// any native Godot call crashes the process.
	[ModuleInitializer]
	internal static void Register()
	{
		EPlugin.RegisterInitializer(new LifeScopeInspectorInitializer());
	}

	public void Initialize(IEPlugin ePlugin)
	{
		var plugin = EditorInterface.Singleton.GetBaseControl().GetParent().GetChildren()
			.OfType<EContainerPlugin>()
			.FirstOrDefault();
		if (plugin == null || plugin.HasMeta(InspectorMeta))
			return;

		var inspector = new LifeScopeInspectorPlugin();
		plugin.AddInspectorPlugin(inspector);
		plugin.SetMeta(InspectorMeta, inspector);

		// Unregistered when the plugin is disabled. The lambda only captures Godot objects, which
		// Godot can serialize, so the connection survives assembly reloads.
		plugin.TreeExiting += () => plugin.RemoveInspectorPlugin(inspector);
	}
}
#endif

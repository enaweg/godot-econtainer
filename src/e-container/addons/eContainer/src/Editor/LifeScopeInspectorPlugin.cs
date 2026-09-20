using Enaweg.Container.Godot;
using Godot;

#if TOOLS
namespace Enaweg.Container.Editor;

public partial class LifeScopeInspectorPlugin : EditorInspectorPlugin
{
	public override bool _CanHandle(GodotObject @object)
	{
		// Match on the type, not the script's file name. The previous check accepted only
		// scripts whose path ended in "LifetimeScope.cs", so a scope written in Combat.cs or
		// GameScope.cs silently got no parent-reference editor.
		if (@object is LifetimeScope)
		{
			return true;
		}

		// Fall back to the file name for objects the editor has not instantiated as their
		// managed type; _ParseProperty still gates on the property actually being there.
		return @object is Node node
			&& (node.GetScript().As<CSharpScript>()?.ResourcePath.EndsWith("LifetimeScope.cs") ?? false);
	}

	public override bool _ParseProperty(GodotObject @object, Variant.Type type, string name, PropertyHint hintType, string hintString, PropertyUsageFlags usageFlags, bool wide)
	{
		if (type == Variant.Type.String && name == "ParentTypeName")
		{
			AddPropertyEditor(name, new ParentReferenceEditorProperty());
			return true;
		}

		return false;
	}
}
#endif

#if TOOLS
using System;
using System.IO;
using Enaweg.Plugin;
using Godot;

namespace Enaweg.Container;

[Tool]
public partial class EContainerPlugin : EditorPlugin, IEEditorPlugin, IInitialize
{
    // Looked up by name rather than referenced: the sources ship hidden as .src and are only
    // compiled once the plugin is enabled, while this file is always compiled.
    internal const string InspectorPluginTypeName = "Enaweg.Container.Editor.LifeScopeInspectorPlugin";

    EditorInspectorPlugin? inspectorPlugin;

    public void CreateRecipe(IEEditorPluginBuilder builder)
    {
        var pluginDirectory = this.GetPluginDirectory()
            ?? throw new InvalidOperationException("The eContainer plugin directory could not be resolved.");
        var nugetSource = Path.Combine(pluginDirectory, ".libs");
        builder
            .AddNuget("VContainer.Standalone", "1.19.0", nugetSource)
            .AddNuget("VContainer.SourceGenerator", "1.19.0", nugetSource)
            .AddDirectory("res://addons/eContainer/.src")
            .AddAutoload("eContainer", "res://addons/eContainer/eContainer.tscn");
    }

    // ePlugin calls this whenever it creates this plugin's context, which includes after the
    // assembly reload that first compiles the sources - _EnterTree has run before that.
    public void Initialize(IEPlugin ePlugin)
    {
        AddLifetimeScopeInspector();
    }

    public override void _EnterTree()
    {
        base._EnterTree();
        AddLifetimeScopeInspector();
    }

    public override void _ExitTree()
    {
        if (inspectorPlugin != null)
        {
            RemoveInspectorPlugin(inspectorPlugin);
            inspectorPlugin = null;
        }

        base._ExitTree();
    }

    public override void _EnablePlugin()
    {
        base._EnablePlugin();
        this.EnableEPlugin();
    }

    public override void _DisablePlugin()
    {
        this.DisableEPlugin();
        base._DisablePlugin();
    }

    /// <summary>Creates the <c>LifetimeScope</c> inspector, or returns <see langword="null"/> while the sources are not compiled.</summary>
    internal static EditorInspectorPlugin? CreateLifetimeScopeInspector()
    {
        Type? type = typeof(EContainerPlugin).Assembly.GetType(InspectorPluginTypeName);
        return type == null ? null : (EditorInspectorPlugin?)Activator.CreateInstance(type);
    }

    void AddLifetimeScopeInspector()
    {
        // The inspector used to be written but never registered, so the parent-type dropdown
        // never showed up in the editor.
        if (inspectorPlugin != null)
            return;

        inspectorPlugin = CreateLifetimeScopeInspector();
        if (inspectorPlugin != null)
        {
            AddInspectorPlugin(inspectorPlugin);
        }
    }
}
#endif

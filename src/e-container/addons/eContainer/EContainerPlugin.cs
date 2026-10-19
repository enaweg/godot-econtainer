#if TOOLS
using System;
using System.IO;
using Enaweg.Plugin;
using Godot;

namespace Enaweg.Container;

[Tool]
public partial class EContainerPlugin : EditorPlugin, IEEditorPlugin
{
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
}
#endif

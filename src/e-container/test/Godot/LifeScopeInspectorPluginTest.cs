#if TOOLS
using Enaweg.Container.Editor;
using Enaweg.Container.Godot;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Godot;

[TestSuite]
[RequireGodotRuntime]
public partial class LifeScopeInspectorPluginTest
{
    // A scope whose name does not end in "LifetimeScope", which the old file-name check
    // required before it would offer the parent-reference editor.
    sealed partial class Combat : LifetimeScope
    {
    }

    [TestCase]
    public void CanHandle_AcceptsAnyLifetimeScope_RegardlessOfScriptFileName()
    {
        // Not AutoFree'd: EditorInspectorPlugin is a RefCounted, and calling Free() on a
        // RefCounted is invalid in Godot.
        var plugin = new LifeScopeInspectorPlugin();

        AssertBool(plugin._CanHandle(AutoFree(new Combat())!)).IsTrue();
        AssertBool(plugin._CanHandle(AutoFree(new LifetimeScope())!)).IsTrue();
    }

    [TestCase]
    public void CanHandle_RejectsNodesThatAreNotScopes()
    {
        // Not AutoFree'd: EditorInspectorPlugin is a RefCounted, and calling Free() on a
        // RefCounted is invalid in Godot.
        var plugin = new LifeScopeInspectorPlugin();

        AssertBool(plugin._CanHandle(AutoFree(new Node())!)).IsFalse();
    }
}
#endif

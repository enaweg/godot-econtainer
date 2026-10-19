#if TOOLS
using System.Collections.Generic;
using System.Linq;
using Enaweg.Container.Editor;
using Enaweg.Container.Godot;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Godot;

// Driven through ShowValue() rather than _UpdateProperty(): binding an edited object with
// SetObjectAndProperty() crashes the engine outside the editor's inspector.
[TestSuite]
[RequireGodotRuntime]
public partial class ParentReferenceEditorPropertyTest
{
    sealed partial class DeclaredParentScope : LifetimeScope
    {
    }

    static OptionButton Show(string value)
    {
        var property = AutoFree(new ParentReferenceEditorProperty())!;
        property.ShowValue(value);
        // Looked up by type: EditorProperty puts a container of its own in front of it.
        return property.GetChildren().OfType<OptionButton>().Single();
    }

    // The type names used to be loaded only when the cached list was null, but it started out
    // as an empty array - so the dropdown never offered a single option.
    [TestCase]
    public void ShowValue_ListsNoneFollowedByTheScopeTypes()
    {
        var options = Show("");

        var texts = new List<string>();
        for (var i = 0; i < options.ItemCount; i++)
        {
            texts.Add(options.GetItemText(i));
        }

        AssertString(texts[0]).IsEqual("None");
        AssertArray(texts).Contains(typeof(RootLifetimeScope).FullName!, typeof(DeclaredParentScope).FullName!);
    }

    [TestCase]
    public void ShowValue_SelectsTheStoredTypeName()
    {
        var options = Show(typeof(DeclaredParentScope).FullName!);

        AssertString(options.GetItemText(options.Selected)).IsEqual(typeof(DeclaredParentScope).FullName!);
    }

    [TestCase]
    public void ShowValue_SelectsNone_WhenNoParentTypeIsDeclared()
    {
        AssertInt(Show("").Selected).IsEqual(0);
    }
}
#endif

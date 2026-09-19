using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Enaweg.Container.Godot;

#if TOOLS
namespace Enaweg.Container.Editor;

public partial class ParentReferenceEditorProperty : EditorProperty
{
    static string[] GetAllTypeNames()
    {
        return new List<string> { "None" }
            .Concat(TypeCache.GetTypesDerivedFrom<LifetimeScope>().Select(type => type.FullName)).ToArray();
    }

    static string GetLabel(Type type) => $"{type.Namespace}/{type.Name}";

    string[] names;
    private OptionButton optionButton = new OptionButton();

    public ParentReferenceEditorProperty()
    {
        AddChild(optionButton);
        AddFocusable(optionButton);

        optionButton.ItemSelected += HandleOptionItemSelected;
    }

    public override void _UpdateProperty()
    {
        if (names == null)
        {
            names = GetAllTypeNames();
        }

        optionButton.Clear();
        foreach (var name in names)
        {
            optionButton.AddItem(name);
        }

        var value = GetEditedObject().Get(GetEditedProperty()).AsString();
        var index = Array.IndexOf(names, value);
        optionButton.Select(index);
    }

    private void HandleOptionItemSelected(long index)
    {
        // EmitChanged routes the edit through the inspector, so it lands in the undo/redo
        // history and marks the scene dirty. Assigning with GetEditedObject().Set() wrote the
        // value straight onto the node and bypassed both.
        EmitChanged(GetEditedProperty(), names[index]);
    }
}
#endif
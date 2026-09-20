using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Enaweg.Container.Godot;

#if TOOLS
namespace Enaweg.Container.Editor;

public partial class ParentReferenceEditorProperty : EditorProperty
{
    // Index 0 is the "no parent declared" entry. It is shown as "None" but stored as an empty
    // string: storing the literal "None" put a name in the scene file that no type will ever
    // match, and only resolved to "no type" because the lookup happened to fail.
    const string NoneLabel = "None";
    const string NoneValue = "";

    static string[] GetAllTypeNames()
    {
        return new List<string> { NoneValue }
            .Concat(TypeCache.GetTypesDerivedFrom<LifetimeScope>()
                .Select(type => type.FullName)
                .Where(name => name is not null)
                .Select(name => name!))
            .ToArray();
    }

    string[] names = [];
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
            optionButton.AddItem(name == NoneValue ? NoneLabel : name);
        }

        var value = GetEditedObject().Get(GetEditedProperty()).AsString();
        var index = Array.IndexOf(names, value ?? NoneValue);

        // A name that is not in the list - a scope type that was renamed or failed to compile -
        // must not silently select "None": that would write the loss back on the next edit.
        // Leaving the button unselected shows the stored value is not one of the options.
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

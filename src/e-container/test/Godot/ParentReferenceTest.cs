using Enaweg.Container.Godot;
using GdUnit4;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Godot;

[TestSuite]
public class ParentReferenceTest
{
    [TestCase]
    public void Create_SetsTypeToGenericArgument()
    {
        var reference = ParentReference.Create<RootLifetimeScope>(typeof(object));

        AssertObject(reference.Type).IsEqual(typeof(RootLifetimeScope));
    }

    [TestCase]
    public void TypeName_Getter_ReflectsCurrentType()
    {
        var reference = ParentReference.Create<RootLifetimeScope>(typeof(object));

        AssertString(reference.TypeName).IsEqual(typeof(RootLifetimeScope).FullName);
    }

    [TestCase]
    public void TypeName_Setter_ResolvesTypeFromAppDomain()
    {
        var reference = new ParentReference
        {
            TypeName = typeof(ActionInstaller).FullName!
        };

        AssertObject(reference.Type).IsEqual(typeof(ActionInstaller));
    }

    [TestCase]
    public void TypeName_SetToNull_ClearsType()
    {
        var reference = ParentReference.Create<RootLifetimeScope>(typeof(object));

        reference.TypeName = null!;

        AssertObject(reference.Type).IsNull();
    }

    [TestCase]
    public void TypeName_SetToUnknownType_LeavesTypeNull()
    {
        var reference = new ParentReference
        {
            TypeName = "Totally.Unknown.Type, NoSuchAssembly"
        };

        AssertObject(reference.Type).IsNull();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void DefaultInstance_HasNoTypeAndNoObject()
    {
        var reference = new ParentReference();

        AssertObject(reference.Type).IsNull();
        AssertObject(reference.Object).IsNull();
    }
}

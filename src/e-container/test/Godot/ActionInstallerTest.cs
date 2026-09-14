using System;
using Enaweg.Container.Godot;
using GdUnit4;
using VContainer;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Godot;

[TestSuite]
public class ActionInstallerTest
{
    [TestCase]
    public void Install_InvokesConfigurationWithGivenBuilder()
    {
        IContainerBuilder? received = null;
        var installer = new ActionInstaller(b => received = b);
        var builder = new ContainerBuilder();

        installer.Install(builder);

        AssertObject(received).IsSame(builder);
    }

    [TestCase]
    public void ImplicitConversion_FromAction_CreatesWorkingInstaller()
    {
        var invoked = false;
        Action<IContainerBuilder> configuration = _ => invoked = true;
        ActionInstaller installer = configuration;

        installer.Install(new ContainerBuilder());

        AssertBool(invoked).IsTrue();
    }
}

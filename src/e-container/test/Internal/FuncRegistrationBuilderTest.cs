using Enaweg.Container.Internal;
using GdUnit4;
using VContainer;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Internal;

[TestSuite]
public class FuncRegistrationBuilderTest
{
    [TestCase]
    public void Build_ResolvesToFuncResult()
    {
        var builder = new ContainerBuilder();
        var registrationBuilder = new FuncRegistrationBuilder(_ => "hello", typeof(string), Lifetime.Singleton);
        builder.Register(registrationBuilder).As(typeof(string));

        using var resolver = builder.Build();

        AssertObject(resolver.Resolve<string>()).IsEqual("hello");
    }

    [TestCase]
    public void Build_PassesResolverIntoImplementationProvider()
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(42);
        var registrationBuilder = new FuncRegistrationBuilder(
            resolver => $"value={resolver.Resolve<int>()}",
            typeof(string),
            Lifetime.Singleton);
        builder.Register(registrationBuilder).As(typeof(string));

        using var resolver = builder.Build();

        AssertObject(resolver.Resolve<string>()).IsEqual("value=42");
    }

    [TestCase]
    public void Build_SingletonLifetime_ReturnsSameInstance()
    {
        var builder = new ContainerBuilder();
        var callCount = 0;
        var registrationBuilder = new FuncRegistrationBuilder(
            _ => new object[] { callCount++ },
            typeof(object[]),
            Lifetime.Singleton);
        builder.Register(registrationBuilder).As(typeof(object[]));

        using var resolver = builder.Build();
        var first = resolver.Resolve<object[]>();
        var second = resolver.Resolve<object[]>();

        AssertObject(first).IsSame(second);
        AssertInt(callCount).IsEqual(1);
    }
}

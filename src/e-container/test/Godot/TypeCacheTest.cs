using System.Linq;
using Enaweg.Container.Godot;
using GdUnit4;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Godot;

[TestSuite]
public class TypeCacheTest
{
    interface IMarkerInterfaceForTypeCacheTest
    {
    }

    abstract class AbstractMarkerImpl : IMarkerInterfaceForTypeCacheTest
    {
    }

    class ConcreteMarkerImplA : IMarkerInterfaceForTypeCacheTest
    {
    }

    class ConcreteMarkerImplB : AbstractMarkerImpl
    {
    }

    [TestCase]
    public void GetTypesDerivedFrom_ReturnsConcreteImplementations_ExcludingAbstractAndSelf()
    {
        var types = TypeCache.GetTypesDerivedFrom<IMarkerInterfaceForTypeCacheTest>();

        AssertArray(types).Contains(typeof(ConcreteMarkerImplA), typeof(ConcreteMarkerImplB));
        AssertBool(types.Contains(typeof(AbstractMarkerImpl))).IsFalse();
        AssertBool(types.Contains(typeof(IMarkerInterfaceForTypeCacheTest))).IsFalse();
    }

    [TestCase]
    public void GetTypesDerivedFrom_IsCachedAcrossCalls()
    {
        var first = TypeCache.GetTypesDerivedFrom<IMarkerInterfaceForTypeCacheTest>();
        var second = TypeCache.GetTypesDerivedFrom<IMarkerInterfaceForTypeCacheTest>();

        AssertObject(first).IsSame(second);
    }
}

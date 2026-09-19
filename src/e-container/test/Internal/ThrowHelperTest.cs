using System;
using Enaweg.Container.Internal;
using GdUnit4;
using static GdUnit4.Assertions;

namespace Enaweg.Container.Tests.Internal;

[TestSuite]
public class ThrowHelperTest
{
    [TestCase]
    public void ThrowArgumentNullIfNull_WithNull_Throws()
    {
        AssertThrown(() => ThrowHelper.ThrowArgumentNullIfNull(null))
            .IsInstanceOf<ArgumentNullException>();
    }

    [TestCase]
    public void ThrowArgumentNullIfNull_WithValue_DoesNotThrow()
    {
        ThrowHelper.ThrowArgumentNullIfNull("value");
    }

    [TestCase]
    public void ThrowObjectDisposedIf_WithTrue_Throws()
    {
        AssertThrown(() => ThrowHelper.ThrowObjectDisposedIf(true, typeof(string)))
            .IsInstanceOf<ObjectDisposedException>();
    }

    [TestCase]
    public void ThrowObjectDisposedIf_WithFalse_DoesNotThrow()
    {
        ThrowHelper.ThrowObjectDisposedIf(false, typeof(string));
    }
}

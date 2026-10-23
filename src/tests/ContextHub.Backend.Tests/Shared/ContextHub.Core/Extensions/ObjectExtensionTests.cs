namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Extensions;

/// <summary>
/// Verifies the shallow CLR property projection without serialization or business normalization.
/// </summary>
public sealed class ObjectExtensionTests
{
    /// <summary>
    /// Rejects a null source rather than fabricating an empty dictionary.
    /// </summary>
    [Fact]
    public void AsDictionary_RejectsNullSource()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => ObjectExtension.AsDictionary(null!));

        Assert.Equal("source", exception.ParamName);
    }

    /// <summary>
    /// Includes inherited and nullable public properties while preserving nested object identity.
    /// </summary>
    [Fact]
    public void AsDictionary_PreservesClrNamesAndShallowValues()
    {
        var source = new PropertySource();

        var result = source.AsDictionary("before_", "_after");

        Assert.Equal(3, result.Count);
        Assert.Equal("Inherited", result["before_BaseName_after"]);
        Assert.Null(result["before_Optional_after"]);
        Assert.Same(source.Child, result["before_Child_after"]);
        Assert.Contains(nameof(PropertySource.Child), source.AsDictionary().Keys);
    }

    /// <summary>
    /// Excludes members which cannot be safely read as ordinary public instance properties.
    /// </summary>
    [Fact]
    public void AsDictionary_ExcludesStaticIndexerAndNonPublicGetters()
    {
        // Excluded getters throw if invoked, making accidental reflection access observable.
        var result = new ExcludedProperties().AsDictionary();

        Assert.Single(result);
        Assert.Equal(42, result[nameof(ExcludedProperties.Visible)]);
    }

    /// <summary>
    /// Propagates the native reflection failure, retaining the original getter exception as its cause.
    /// </summary>
    [Fact]
    public void AsDictionary_DoesNotSwallowGetterFailures()
    {
        var source = new ThrowingProperty();

        var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() => source.AsDictionary());

        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    /// <summary>
    /// Rejects ambiguous CLR property names instead of silently choosing or overwriting a value.
    /// </summary>
    [Fact]
    public void AsDictionary_RejectsDuplicateKeys()
    {
        // Different return types retain both hidden properties in the native reflection result.
        Assert.Throws<ArgumentException>(() => new DuplicateProperty().AsDictionary());
    }

    /// <summary>
    /// Supplies a public inherited property for the shallow projection.
    /// </summary>
    private class BaseProperties
    {
        /// <summary>
        /// Retains its original CLR name when discovered on a derived instance.
        /// </summary>
        public string BaseName => "Inherited";
    }

    /// <summary>
    /// Supplies nullable and nested values without recursive conversion.
    /// </summary>
    private sealed class PropertySource : BaseProperties
    {
        /// <summary>
        /// Represents a readable property with an absent value.
        /// </summary>
        public string? Optional => null;

        /// <summary>
        /// Holds an object whose reference must survive conversion unchanged.
        /// </summary>
        public object Child { get; } = new();
    }

    /// <summary>
    /// Exposes only one property eligible for the approved projection.
    /// </summary>
    private sealed class ExcludedProperties
    {
        /// <summary>
        /// Provides the eligible public instance getter.
        /// </summary>
        public int Visible => 42;

        /// <summary>
        /// Fails if the helper incorrectly invokes a static getter.
        /// </summary>
        public static int StaticValue => throw new InvalidOperationException();

        /// <summary>
        /// Fails if the helper incorrectly reads an indexed property.
        /// </summary>
        /// <param name="index">The unused index required by this excluded member.</param>
        /// <returns>No value; reading this member is an error in the fixture.</returns>
        public int this[int index] => throw new InvalidOperationException();

        /// <summary>
        /// Has a public setter but no public getter and must therefore be excluded.
        /// </summary>
        public int PrivateGetter
        {
            private get => throw new InvalidOperationException();
            set { }
        }

        /// <summary>
        /// Has no getter at all and must not enter the dictionary.
        /// </summary>
        public int WriteOnly { set { } }
    }

    /// <summary>
    /// Supplies a getter whose original exception must not be suppressed.
    /// </summary>
    private sealed class ThrowingProperty
    {
        /// <summary>
        /// Throws a fixture exception when reflection reads the property.
        /// </summary>
        public int Value => throw new InvalidOperationException();
    }

    /// <summary>
    /// Supplies the inherited half of an ambiguous CLR name.
    /// </summary>
    private class DuplicateBase
    {
        /// <summary>
        /// Uses the same name as the derived property but a different return type.
        /// </summary>
        public int Value => 1;
    }

    /// <summary>
    /// Hides a property without removing its reflection-visible base definition.
    /// </summary>
    private sealed class DuplicateProperty : DuplicateBase
    {
        /// <summary>
        /// Produces the duplicate dictionary key when both properties are enumerated.
        /// </summary>
        public new string Value => "Derived";
    }
}

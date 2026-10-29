namespace ContextHub.Shared.Extensions;

/// <summary>
/// Projects readable CLR properties into a shallow dictionary without serialization or normalization.
/// </summary>
public static class ObjectExtension
{
    /// <summary>
    /// Reads public instance properties, including inherited properties, without converting nested values.
    /// </summary>
    /// <param name="source">The non-null object whose public getters will be invoked.</param>
    /// <param name="prefix">The optional text prepended to each CLR property name.</param>
    /// <param name="suffix">The optional text appended to each CLR property name.</param>
    /// <returns>A new dictionary retaining nullable values and nested object references.</returns>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="ArgumentException">Multiple properties produce the same dictionary key.</exception>
    /// <exception cref="TargetInvocationException">A reflected property getter throws.</exception>
    public static IDictionary<string, object?> AsDictionary(
        this object source,
        string? prefix = null,
        string? suffix = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        // A public setter does not make its private getter readable; indexers require arguments and are excluded.
        var properties = source.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetMethod is { IsPublic: true, IsStatic: false } &&
                               property.GetIndexParameters().Length == 0);

        // Native dictionary construction preserves references and propagates getter failures or ambiguous keys.
        return properties.ToDictionary(
            property => $"{prefix}{property.Name}{suffix}",
            property => property.GetValue(source),
            StringComparer.Ordinal);
    }
}

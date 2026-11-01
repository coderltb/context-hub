namespace ContextHub.ApiCommon.Attributes.OpenApi;

/// <summary>
/// Overrides one explicitly named binding without changing reusable DTO or component defaults.
/// </summary>
/// <param name="providerType">The provider explicitly registered in DI.</param>
/// <param name="target">The actual native binding location.</param>
/// <param name="name">The binding name, including any FromQuery/FromForm alias.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public abstract class OpenApiValueExampleAttribute(Type providerType, OpenApiValueTarget target, string name) : Attribute
{
    /// <summary>
    /// Gets the explicitly selected provider type.
    /// </summary>
    public Type ProviderType { get; } = providerType;

    /// <summary>
    /// Gets the declared binding location.
    /// </summary>
    public OpenApiValueTarget Target { get; } = target;

    /// <summary>
    /// Gets the exact native binding name rather than a guessed JSON property name.
    /// </summary>
    public string Name { get; } = name;
}

/// <summary>
/// Selects a typed provider for one operation-local query/path/header/form value.
/// </summary>
/// <typeparam name="TProvider">The synthetic provider registered in DI.</typeparam>
/// <param name="target">The actual native binding location.</param>
/// <param name="name">The explicit binding name.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class OpenApiValueExampleAttribute<TProvider>(
    OpenApiValueTarget target,
    string name)
    : OpenApiValueExampleAttribute(typeof(TProvider), target, name) where TProvider : IOpenApiExampleProvider;

namespace ContextHub.ApiCommon.OpenApi.Attributes;

/// <summary>
/// Identifies the explicitly selected provider for an API JSON request example.
/// </summary>
/// <param name="providerType">The example provider type activated through dependency injection.</param>
[AttributeUsage(AttributeTargets.Method)]
public abstract class OpenApiRequestExampleAttribute(Type providerType) : Attribute
{
    /// <summary>
    /// Gets the provider type selected by endpoint metadata, without assembly discovery.
    /// </summary>
    public Type ProviderType { get; } = providerType;
}

/// <summary>
/// Attaches a strongly typed synthetic request example to an API operation.
/// </summary>
/// <typeparam name="TProvider">The example provider registered in dependency injection.</typeparam>
public sealed class OpenApiRequestExampleAttribute<TProvider>() : OpenApiRequestExampleAttribute(typeof(TProvider))
    where TProvider : IOpenApiExampleProvider;

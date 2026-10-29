namespace ContextHub.ApiCommon.Attributes.OpenApi;

/// <summary>
/// Identifies one named example for an already documented API response status.
/// </summary>
/// <param name="statusCode">The response status associated with this example.</param>
/// <param name="providerType">The example provider type activated through dependency injection.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public abstract class OpenApiResponseExampleAttribute(int statusCode, Type providerType) : Attribute
{
    /// <summary>
    /// Gets the response status to enrich; this metadata never creates an additional response contract.
    /// </summary>
    public int StatusCode { get; } = statusCode;

    /// <summary>
    /// Gets the explicitly registered fixture provider.
    /// </summary>
    public Type ProviderType { get; } = providerType;

    /// <summary>
    /// Gets or sets the unique example name within the response status.
    /// </summary>
    public string Name { get; set; } = ApiCommonOpenApiConstants.DEFAULT_EXAMPLE_NAME;
}

/// <summary>
/// Attaches a strongly typed named response example without merging different response shapes.
/// </summary>
/// <typeparam name="TProvider">The example provider registered in dependency injection.</typeparam>
/// <param name="statusCode">The already documented HTTP response status.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class OpenApiResponseExampleAttribute<TProvider>(int statusCode)
    : OpenApiResponseExampleAttribute(statusCode, typeof(TProvider))
    where TProvider : IOpenApiExampleProvider;

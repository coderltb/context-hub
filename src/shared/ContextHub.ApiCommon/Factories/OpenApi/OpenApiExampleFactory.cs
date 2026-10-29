namespace ContextHub.ApiCommon.Factories.OpenApi;

/// <summary>
/// Builds synthetic JSON fixtures for body and bound-value examples without coupling their operation transformers.
/// </summary>
internal static class OpenApiExampleFactory
{
    /// <summary>
    /// Serializes only an explicitly registered synthetic provider with the endpoint's native settings.
    /// </summary>
    /// <param name="providerType">The endpoint-selected provider type.</param>
    /// <param name="services">The document generation scope.</param>
    /// <param name="options">The host's request and response JSON settings.</param>
    /// <param name="expectedType">The exact native wire type used to check overrides.</param>
    /// <returns>A detached JSON node for cloning into media entries.</returns>
    internal static JsonNode Create(
        Type providerType,
        IServiceProvider services,
        JsonSerializerOptions options,
        Type? expectedType)
    {
        // Resolve only host-registered providers; Activator or assembly discovery would bypass the explicit allowlist.
        if (services.GetService(providerType) is not IOpenApiExampleProvider provider)
        {
            throw new InvalidOperationException(ApiCommonErrorConstants.OpenApiErrors.PROVIDER_NOT_REGISTERED);
        }

        var value = provider.GetExample();

        // The generic boundary guarantees registration type safety; verify attribute overrides against native metadata too.
        if (value is not null && expectedType is not null && expectedType != typeof(void) &&
            !expectedType.IsInstanceOfType(value))
        {
            throw new InvalidOperationException(ApiCommonErrorConstants.OpenApiErrors.INVALID_EXAMPLE_TYPE);
        }

        // Use actual MVC/HTTP JSON settings, including converters and naming policies, rather than serializer defaults.
        return JsonSerializer.SerializeToNode(value, value?.GetType() ?? typeof(object), options)
            ?? throw new InvalidOperationException(ApiCommonErrorConstants.OpenApiErrors.EMPTY_EXAMPLE);
    }
}

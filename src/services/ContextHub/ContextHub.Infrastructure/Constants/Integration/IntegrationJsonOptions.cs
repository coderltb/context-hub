namespace ContextHub.Infrastructure.Constants.Integration;

/// <summary>
/// Keeps the stored canonical configuration aligned with its closed camel-case contract.
/// </summary>
public static class IntegrationJsonOptions
{
    /// <summary>
    /// Reuses one immutable native serializer configuration for internal canonical configuration.
    /// </summary>
    public static readonly JsonSerializerOptions CANONICAL = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

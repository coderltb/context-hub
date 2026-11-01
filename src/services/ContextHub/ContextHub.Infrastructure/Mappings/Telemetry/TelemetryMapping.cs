namespace ContextHub.Infrastructure.Mappings.Telemetry;

/// <summary>
/// Registers reusable approved feature projections once through global Mapster discovery.
/// </summary>
public sealed class TelemetryMapping : IRegister
{
    /// <summary>
    /// Maps editable fields and safe response state without granting ownership of audit metadata to requests.
    /// </summary>
    /// <param name="config">The startup-owned global configuration.</param>
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<CreateRecommendationRequestDto, Recommendation>()
            .Map(value => value.Type, value => value.RecommendationType)
            .Map(value => value.Title, value => value.Title.Trim())
            .Map(value => value.Description, value => value.Description.Trim())
            .Ignore(value => value.PackageId!);

        config.NewConfig<Recommendation, RecommendationResponseDto>()
            .Map(value => value.RecommendationType, value => value.Type)
            .Map(
                value => value.ConcurrencyToken,
                value => PersistenceConcurrencyHelper.Encode(value.ConcurrencyVersion))
            .Map(value => value.Context, value => ParseContext(value.Context))
            .Ignore(value => value.PackageId!);
    }

    /// <summary>
    /// Clones only server-derived coded JSON so no disposable document escapes into the response.
    /// </summary>
    /// <param name="context">The stored coded context.</param>
    /// <returns>The detached JSON value, or null.</returns>
    private static JsonElement? ParseContext(string? context) =>
        // JSON null remains a JSON value; only a missing database value becomes nullable null.
        context is null ? null : JsonSerializer.Deserialize<JsonElement>(context);
}

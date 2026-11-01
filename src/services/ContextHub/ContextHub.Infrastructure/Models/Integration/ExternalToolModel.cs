namespace ContextHub.Infrastructure.Models.Integration;

/// <summary>
/// Holds one private upstream definition and its exact reviewed fingerprint.
/// </summary>
/// <param name="Name">The private upstream tool name.</param>
/// <param name="Title">The upstream preview title.</param>
/// <param name="Description">The upstream preview description.</param>
/// <param name="InputSchema">The advertised input schema.</param>
/// <param name="DefinitionHash">The hash bound to the full advertised definition.</param>
/// <param name="ReadOnlyHint">Whether the upstream advertises read-only behavior.</param>
public sealed record ExternalToolModel(
    string Name,
    string? Title,
    string? Description,
    JsonElement InputSchema,
    string DefinitionHash,
    bool ReadOnlyHint)
{
    /// <summary>
    /// Gets the optional reviewed native structured-output schema.
    /// </summary>
    public JsonElement? OutputSchema { get; init; }

    /// <summary>
    /// Gets the rebuildable embedding of neutral reviewed metadata.
    /// </summary>
    public float[]? Embedding { get; init; }

    /// <summary>
    /// Gets the exact model used for the metadata embedding.
    /// </summary>
    public string? EmbeddingModel { get; init; }
}

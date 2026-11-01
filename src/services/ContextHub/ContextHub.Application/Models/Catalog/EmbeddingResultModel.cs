namespace ContextHub.Application.Models.Catalog;

/// <summary>
/// Associates a normalized local vector with its pinned model and preprocessing identity.
/// </summary>
/// <param name="Values">The normalized 384-dimensional CPU inference result.</param>
/// <param name="Model">The checkpoint, revision and preprocessing identity used for compatibility checks.</param>
public sealed record EmbeddingResultModel(float[] Values, string Model);

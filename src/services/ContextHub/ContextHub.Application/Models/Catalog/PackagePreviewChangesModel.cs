namespace ContextHub.Application.Models.Catalog;

/// <summary>
/// Carries factual field changes separately from the consumer areas those changes affect.
/// </summary>
/// <param name="Items">Deterministically ordered changed contract fields.</param>
/// <param name="Areas">Distinct public contract areas, never inferred quality scores.</param>
public sealed record PackagePreviewChangesModel(JsonArray Items, JsonArray Areas);

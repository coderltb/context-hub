namespace ContextHub.Application.Models.Catalog;

/// <summary>
/// Carries exact source content for strict parsing; raw content is never returned as a public manifest.
/// </summary>
/// <param name="ManifestJson">The unmodified internal manifest text, including source provenance.</param>
/// <param name="BundleJson">The unmodified consumer guidance text to validate before publication.</param>
/// <param name="CommitSha">The actual immutable commit resolved by the source adapter.</param>
public sealed record PackageSourceModel(string ManifestJson, string BundleJson, string CommitSha);

namespace ContextHub.Infrastructure.Models.Catalog;

/// <summary>
/// Holds parsed source only after strict canonicalization, schema and identity checks.
/// </summary>
/// <param name="Manifest">The internal immutable manifest.</param>
/// <param name="Bundle">The validated package guidance.</param>
/// <param name="ContentHash">The canonical immutable-content hash.</param>
/// <param name="SearchText">The allowlisted public search passage.</param>
/// <param name="Findings">Safe preview findings without raw source values.</param>
public sealed record ValidatedPackageModel(
    JsonObject? Manifest,
    JsonObject? Bundle,
    string? ContentHash,
    string SearchText,
    IReadOnlyList<PreviewFindingResponseDto> Findings)
{
    /// <summary>
    /// Reports whether publication is permitted by every required content check.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Manifest), nameof(Bundle), nameof(ContentHash))]
    public bool IsValid => Manifest is not null && Bundle is not null && ContentHash is not null &&
        Findings.All(value => value.Severity != "error");
}

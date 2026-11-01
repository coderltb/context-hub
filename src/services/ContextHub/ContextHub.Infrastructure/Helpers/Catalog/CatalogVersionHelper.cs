namespace ContextHub.Infrastructure.Helpers.Catalog;

/// <summary>
/// Restricts the native Semver parser to the catalog's stable-only contract.
/// </summary>
public static class CatalogVersionHelper
{
    /// <summary>
    /// Determines whether a version is canonical stable X.Y.Z within the contract length.
    /// </summary>
    /// <param name="value">The proposed exact version.</param>
    /// <returns>True only for canonical stable versions.</returns>
    public static bool IsStable(string? value) =>
        value is { Length: > 0 and <= 64 } &&
        SemVersion.TryParse(value, SemVersionStyles.Strict, out var version) &&
        !version.IsPrerelease && version.Metadata.Length == 0 &&
        version.ToString() == value;

    /// <summary>
    /// Compares validated stable versions numerically, including components larger than Int32.
    /// </summary>
    /// <param name="left">The first validated version.</param>
    /// <param name="right">The second validated version.</param>
    /// <returns>The native semantic precedence comparison.</returns>
    public static int Compare(string left, string right) =>
        SemVersion.ComparePrecedence(
            SemVersion.Parse(left, SemVersionStyles.Strict),
            SemVersion.Parse(right, SemVersionStyles.Strict));
}

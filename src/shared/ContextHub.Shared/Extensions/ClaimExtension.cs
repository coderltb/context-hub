namespace ContextHub.Shared.Extensions;

/// <summary>
/// Normalizes caller-selected claims without validating tokens, authenticating users or assigning application permissions.
/// </summary>
public static class ClaimExtension
{
    /// <summary>
    /// Copies a usable source claim into a destination alias, replacing existing aliases without changing the source identity.
    /// </summary>
    /// <param name="source">The identity containing the original claim.</param>
    /// <param name="claims">The separate destination list to normalize.</param>
    /// <param name="fromType">The source claim type selected by the caller.</param>
    /// <param name="toType">The destination alias type to replace.</param>
    public static void ReplaceClaim(this ClaimsIdentity source, List<Claim> claims, string fromType, string toType)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentException.ThrowIfNullOrWhiteSpace(fromType);
        ArgumentException.ThrowIfNullOrWhiteSpace(toType);

        // Missing source data must not erase an alias that the caller already supplied.
        var original = source.FindFirst(fromType);
        if (original is null || string.IsNullOrWhiteSpace(original.Value))
        {
            return;
        }

        // Claim.Type is immutable. Copy issuer metadata and properties into an unbound alias;
        // the destination ClaimsIdentity owns its Subject when the caller adds or rebuilds that identity.
        var alias = new Claim(toType, original.Value, original.ValueType, original.Issuer, original.OriginalIssuer);
        foreach (var property in original.Properties)
        {
            alias.Properties.Add(property.Key, property.Value);
        }

        // Only the destination list changes, including when source and alias use the same type.
        claims.RemoveAll(claim => string.Equals(claim.Type, toType, StringComparison.Ordinal));
        claims.Add(alias);
    }

    /// <summary>
    /// Adds a claim only when the exact ordinal type and value pair is absent from the destination identity.
    /// </summary>
    /// <param name="identity">The identity receiving the claim; authentication state is not changed.</param>
    /// <param name="claimType">The claim type to preserve without recasing.</param>
    /// <param name="claimValue">The claim value to preserve without normalization.</param>
    public static void EnsureClaim(this ClaimsIdentity identity, string claimType, string claimValue)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimType);
        ArgumentNullException.ThrowIfNull(claimValue);

        // The native predicate overload preserves exact vocabulary rather than applying type-name case folding.
        if (identity.HasClaim(claim => string.Equals(claim.Type, claimType, StringComparison.Ordinal) &&
                                       string.Equals(claim.Value, claimValue, StringComparison.Ordinal)))
        {
            return;
        }

        identity.AddClaim(new Claim(claimType, claimValue));
    }
}

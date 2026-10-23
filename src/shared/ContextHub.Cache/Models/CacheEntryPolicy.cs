namespace ContextHub.Cache.Models;

/// <summary>
/// Defines bounded L1/L2 expiration; does not authorize caching lifecycle or credential decisions.
/// </summary>
public sealed record CacheEntryPolicy
{
    /// <summary>
    /// Creates a policy with local expiration no longer than shared expiration.
    /// </summary>
    /// <param name="expiration">The maximum shared-cache lifetime.</param>
    /// <param name="localExpiration">The maximum local-cache lifetime.</param>
    /// <param name="tags">Optional trusted capability-owned invalidation groups.</param>
    /// <param name="flags">Optional native read/write bypass flags; null keeps the host defaults.</param>
    public CacheEntryPolicy(TimeSpan expiration, TimeSpan localExpiration, IEnumerable<string>? tags = null,
        HybridCacheEntryFlags? flags = null)
    {
        // L1 must not outlive L2; otherwise one host could serve older data after the shared entry expires.
        if (expiration <= TimeSpan.Zero || localExpiration <= TimeSpan.Zero || localExpiration > expiration)
        {
            throw new ArgumentOutOfRangeException(nameof(expiration), CacheErrorConstants.PolicyErrors.EXPIRATION_INVALID);
        }

        Expiration = expiration;
        LocalExpiration = localExpiration;
        Flags = flags;

        // Materialize and hide the mutable array: later caller edits must not change an entry's invalidation groups.
        var snapshot = tags?.ToArray() ?? [];
        foreach (var tag in snapshot)
        {
            ValidateTag(tag);
        }

        Tags = Array.AsReadOnly(snapshot.Distinct(StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// Gets the immutable ordinal-distinct invalidation groups.
    /// </summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>
    /// Validates an exact tag consistently for writes and invalidation.
    /// </summary>
    /// <param name="tag">The trusted invalidation identifier.</param>
    internal static void ValidateTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag) || tag.Any(char.IsWhiteSpace) || tag.Contains('*'))
        {
            throw new ArgumentException(CacheErrorConstants.PolicyErrors.TAG_INVALID, nameof(tag));
        }
    }

    /// <summary>
    /// Gets the shared-cache lifetime.
    /// </summary>
    public TimeSpan Expiration { get; }

    /// <summary>
    /// Gets the local-cache lifetime.
    /// </summary>
    public TimeSpan LocalExpiration { get; }

    /// <summary>
    /// Gets native read/write controls independently of tag-based invalidation.
    /// </summary>
    public HybridCacheEntryFlags? Flags { get; }

    /// <summary>
    /// Projects validated lifetimes into native options without adding another expiration policy.
    /// </summary>
    /// <returns>The options used for this exact cache operation.</returns>
    internal HybridCacheEntryOptions ToOptions() => new()
    {
        Expiration = Expiration,
        LocalCacheExpiration = LocalExpiration,
        Flags = Flags
    };
}

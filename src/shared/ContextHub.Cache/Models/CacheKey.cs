namespace ContextHub.Cache.Models;

/// <summary>
/// Names a trusted, bounded cache entry with unambiguous ordered parts and a schema version.
/// </summary>
public sealed record CacheKey
{
    /// <summary>
    /// Creates a key only after the public factory has validated its namespace and encoded parts.
    /// </summary>
    /// <param name="cacheNamespace">The validated capability namespace.</param>
    /// <param name="value">The opaque storage key.</param>
    private CacheKey(
        string cacheNamespace,
        string value)
    {
        Namespace = cacheNamespace;
        Value = value;
    }

    /// <summary>
    /// Gets the non-sensitive capability namespace.
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// Gets the opaque key passed to HybridCache; hashing is not permission to cache credentials.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates a deterministic key; callers must supply validated identifiers, not raw user input or credentials.
    /// </summary>
    /// <param name="cacheNamespace">The capability-owned lowercase namespace.</param>
    /// <param name="schemaVersion">A positive cache payload schema version.</param>
    /// <param name="parts">Trusted identifying values in fixed, meaningful order.</param>
    /// <returns>A bounded key with a SHA-256 suffix; values are not reversible plaintext key segments.</returns>
    public static CacheKey Create(
        string cacheNamespace,
        int schemaVersion,
        params string[] parts)
    {
        if (string.IsNullOrEmpty(cacheNamespace) || cacheNamespace.Length > CacheConstants.KeyVariables.MAX_NAMESPACE_LENGTH ||
            !cacheNamespace.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '-'))
        {
            throw new ArgumentException(CacheErrorConstants.PolicyErrors.NAMESPACE_INVALID, nameof(cacheNamespace));
        }

        if (schemaVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), CacheErrorConstants.PolicyErrors.VERSION_INVALID);
        }

        ArgumentNullException.ThrowIfNull(parts);
        // Runtime input may contain null elements despite the caller-facing non-null contract.
        IEnumerable<string?> rawParts = parts;
        if (parts.Length is < 1 or > CacheConstants.KeyVariables.MAX_PARTS ||
            rawParts.Any(part => part is null || part.Length > CacheConstants.KeyVariables.MAX_PART_LENGTH))
        {
            throw new ArgumentException(CacheErrorConstants.PolicyErrors.PARTS_INVALID, nameof(parts));
        }

        // Reject invalid UTF-16 before JSON encoding can replace it and make distinct inputs share a key.
        var encoding = new UTF8Encoding(false, true);
        foreach (var part in parts)
        {
            encoding.GetByteCount(part);
        }

        // JSON preserves ordered part boundaries; joining with a delimiter could create ambiguous keys.
        var hash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(parts)));
        return new CacheKey(
            cacheNamespace,
            $"{CacheConstants.KeyVariables.PREFIX}:{cacheNamespace}:v{schemaVersion.ToString(CultureInfo.InvariantCulture)}:{hash}");
    }
}

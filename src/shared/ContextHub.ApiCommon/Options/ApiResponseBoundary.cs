namespace ContextHub.ApiCommon.Options;

/// <summary>
/// Defines the immutable route scope and public response version supplied by one API host.
/// </summary>
public sealed class ApiResponseBoundary
{
    /// <summary>
    /// Validates explicit boundary configuration without assuming a product or service prefix.
    /// </summary>
    /// <param name="routePrefix">The non-root absolute route prefix with complete, nonempty segments.</param>
    /// <param name="apiVersion">The nonblank public response version.</param>
    public ApiResponseBoundary(
        string routePrefix,
        string apiVersion)
    {
        // Reject ambiguous paths rather than broadening response adaptation to unrelated protocols.
        if (string.IsNullOrWhiteSpace(routePrefix) || !routePrefix.StartsWith('/') ||
            routePrefix == "/" || routePrefix.Any(char.IsWhiteSpace) ||
            routePrefix.IndexOfAny(['?', '#', '\\']) >= 0 ||
            routePrefix.Split('/').Skip(1).Any(segment => segment.Length == 0))
        {
            throw new ArgumentException(ApiCommonErrorConstants.BoundaryErrors.INVALID_PREFIX, nameof(routePrefix));
        }

        if (string.IsNullOrWhiteSpace(apiVersion))
        {
            throw new ArgumentException(ApiCommonErrorConstants.BoundaryErrors.VERSION_REQUIRED, nameof(apiVersion));
        }

        RoutePrefix = new PathString(routePrefix);
        ApiVersion = apiVersion;
    }

    /// <summary>
    /// Gets the exact route scope owned by this host.
    /// </summary>
    public PathString RoutePrefix { get; }

    /// <summary>
    /// Gets the host's public envelope metadata version.
    /// </summary>
    public string ApiVersion { get; }

    /// <summary>
    /// Matches complete route segments, excluding similar names such as v1 and v10.
    /// </summary>
    /// <param name="path">The request or documented operation path.</param>
    /// <returns>Whether the path belongs to this host's response boundary.</returns>
    public bool Contains(PathString path) => path.StartsWithSegments(RoutePrefix);
}

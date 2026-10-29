namespace ContextHub.Authentication.Application.Interfaces.Roles;

/// <summary>
/// Provides provisioned role metadata and live native role checks without exposing Identity records.
/// </summary>
public interface IRoleService
{
    /// <summary>
    /// Gets approved role metadata with the shared cache policy.
    /// </summary>
    /// <param name="cancellationToken">Cancels the query or cache access.</param>
    /// <returns>The provisioned role projections.</returns>
    Task<IReadOnlyList<RoleResponseDto>> GetRolesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Checks native role existence independently of the metadata cache.
    /// </summary>
    /// <param name="roleName">The selected role name.</param>
    /// <returns>Whether the role currently exists.</returns>
    Task<bool> ExistsAsync(string roleName);

    /// <summary>
    /// Invalidates role metadata only after provisioning has committed.
    /// </summary>
    /// <param name="cancellationToken">Cancels invalidation.</param>
    /// <returns>The cache invalidation operation.</returns>
    ValueTask InvalidateCacheAsync(CancellationToken cancellationToken);
}

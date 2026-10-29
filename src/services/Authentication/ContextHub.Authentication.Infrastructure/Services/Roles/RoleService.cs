namespace ContextHub.Authentication.Infrastructure.Services.Roles;

/// <summary>
/// Reads provisioned role metadata and delegates role-name normalization/existence to native Identity.
/// </summary>
/// <param name="roleManager">The native role manager used for live existence checks.</param>
/// <param name="userRepository">The mapped metadata query boundary.</param>
/// <param name="cacheService">The explicitly selected role metadata cache, never an authorization authority.</param>
public sealed class RoleService(
    RoleManager<IdentityRole<Guid>> roleManager,
    IUserRepository userRepository,
    ICacheService cacheService) : IRoleService
{
    /// <summary>
    /// Gets the role projections from the feature query boundary.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The safe role projections.</returns>
    public async Task<IReadOnlyList<RoleResponseDto>> GetRolesAsync(CancellationToken cancellationToken)
    {
        // Cache display metadata only; authorization checks use live native role/account state through separate methods.
        var key = CacheKey.Create(
            AuthenticationRecoveryConstants.ROLES_CACHE_NAMESPACE,
            AuthenticationRecoveryConstants.KEY_VERSION,
            AuthenticationRecoveryConstants.ROLES_KEY);
        var cached = await cacheService.GetAsync<RoleResponseDto[]>(key, cancellationToken);

        // A cached DTO array is safe to return because it contains no request correlation metadata or tracked entity state.
        if (cached is not null)
        {
            return cached;
        }

        // This tiny scoped read is awaited by its request; avoid a coalesced factory capturing a request-owned context.
        var rows = (await userRepository.GetRolesAsync(cancellationToken)).ToArray();
        await cacheService.SetAsync(key, rows, cancellationToken: cancellationToken);

        return rows;
    }

    /// <summary>
    /// Checks current role existence through the manager's configured normalizer without consulting cached metadata.
    /// </summary>
    /// <param name="roleName">The role selected by the account workflow.</param>
    /// <returns>Whether native Identity currently recognizes the role.</returns>
    public Task<bool> ExistsAsync(string roleName) => roleManager.RoleExistsAsync(roleName);

    /// <summary>
    /// Removes published role metadata after the transaction that provisioned those roles has committed.
    /// </summary>
    /// <param name="cancellationToken">Cancels cache invalidation.</param>
    /// <returns>The metadata invalidation operation.</returns>
    public ValueTask InvalidateCacheAsync(CancellationToken cancellationToken) =>
        cacheService.RemoveAsync(CacheKey.Create(AuthenticationRecoveryConstants.ROLES_CACHE_NAMESPACE,
            AuthenticationRecoveryConstants.KEY_VERSION, AuthenticationRecoveryConstants.ROLES_KEY), cancellationToken);
}

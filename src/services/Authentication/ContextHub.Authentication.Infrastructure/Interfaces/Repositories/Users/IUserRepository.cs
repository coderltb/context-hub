namespace ContextHub.Authentication.Infrastructure.Interfaces.Repositories.Users;

/// <summary>
/// Extends native entity operations inside Infrastructure; Application and HTTP consume only the service port and DTOs.
/// </summary>
public interface IUserRepository : IGenericRepository<User>
{
    /// <summary>
    /// Gets the approved account page and current memberships.
    /// </summary>
    /// <param name="request">The grouped public list inputs.</param>
    /// <param name="cancellationToken">Cancels the sequential reads.</param>
    /// <returns>The approved account page.</returns>
    Task<PageResponse<UserResponseDto>> GetPageAsync(
        GetUsersRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets one approved account projection.
    /// </summary>
    /// <param name="id">The target account.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The projection, or null.</returns>
    Task<UserResponseDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Gets provisioned role metadata through the configured projection.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The safe role projections.</returns>
    Task<IReadOnlyList<RoleResponseDto>> GetRolesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets an active Owner for protocol validation using current database state.
    /// </summary>
    /// <param name="subject">The authenticated protocol subject.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The minimal account model, or null for an invalid or no-longer-permitted subject.</returns>
    Task<UserAccountModel?> GetPermittedUserAsync(
        string? subject,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the exact native Identity logout marker without using tracked token state.
    /// </summary>
    /// <param name="userId">The authenticated account.</param>
    /// <param name="sessionId">The protected browser-session identifier.</param>
    /// <param name="cancellationToken">Cancels the authoritative marker read.</param>
    /// <returns>Whether this session has been revoked.</returns>
    Task<bool> IsSessionRevokedAsync(Guid userId, string sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Acquires the Owner-membership invariant lock inside the caller's existing transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancels lock acquisition.</param>
    /// <returns>The completed lock acquisition.</returns>
    Task LockOwnerMembershipAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Resolves provisioned role names from the complete UUID selection.
    /// </summary>
    /// <param name="ids">The selected role IDs.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The native role names, or a rejected invalid selection.</returns>
    Task<string[]> ResolveRoleNamesAsync(Guid[] ids, CancellationToken cancellationToken);

    /// <summary>
    /// Checks current membership without treating token role claims as the database authority.
    /// </summary>
    /// <param name="excluded">The account being changed.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>Whether another active Owner exists.</returns>
    Task<bool> HasAnotherActiveOwnerAsync(Guid excluded, CancellationToken cancellationToken);
}

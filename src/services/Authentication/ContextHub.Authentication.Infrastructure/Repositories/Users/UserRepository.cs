namespace ContextHub.Authentication.Infrastructure.Repositories.Users;

/// <summary>
/// Uses inherited typed queries and keeps Identity table access inside the feature persistence boundary.
/// </summary>
/// <param name="authenticationDbContext">Authentication's scoped context.</param>
/// <param name="logger">The inherited repository metadata logger.</param>
public class UserRepository(
    AuthenticationDbContext authenticationDbContext,
    ILogger<UserRepository> logger)
    : GenericRepository<User>(authenticationDbContext, logger), IUserRepository
{
    /// <summary>
    /// Translates the approved list DTO into native typed search and multi-field ordering.
    /// </summary>
    /// <param name="request">The grouped page, search, filter and ordering inputs.</param>
    /// <param name="cancellationToken">Cancels the sequential database reads.</param>
    /// <returns>The approved account page with its current role memberships.</returns>
    public virtual async Task<PageResponse<UserResponseDto>> GetPageAsync(
        GetUsersRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!request.Sort.TryParseUserSort(out var fields))
        {
            throw new BusinessException(AuthenticationErrorConstants.INVALID_SORT, AuthenticationErrorConstants.INVALID_SORT_MESSAGE);
        }

        QueryOrder<User>? order = null;
        foreach (var field in fields)
        {
            // Public aliases are explicitly mapped to typed selectors; no consumer property path enters EF.
            order = field.Field switch
            {
                AuthenticationQueryConstants.USER_NAME => AppendOrder(order, value => value.UserName, field.Descending),
                AuthenticationQueryConstants.EMAIL => AppendOrder(order, value => value.Email, field.Descending),
                AuthenticationQueryConstants.IS_ACTIVE => AppendOrder(order, value => value.IsActive, field.Descending),
                AuthenticationQueryConstants.CREATED_AT => AppendOrder(order, value => value.CreatedAt, field.Descending),
                AuthenticationQueryConstants.UPDATED_AT => AppendOrder(order, value => value.UpdatedAt, field.Descending),
                _ => throw new InvalidOperationException(AuthenticationErrorConstants.UNSUPPORTED_PARSED_SORT_FIELD)
            };
        }

        // A final unique key stabilizes an explicitly requested order; blank sort does not silently add ordering.
        order = order?.ThenBy(value => value.Id);
        var page = new PageRequest(request.Page, request.PageSize);
        var search = new SearchOptions<User>
        {
            SearchTerm = request.Query,
            TextFields = [value => value.UserName, value => value.Email],
            FieldFilters = [request.IsActive is { } active ? value => value.IsActive == active : null]
        };
        var result = await GetPageAsync<UserResponseDto>(page, order, search: search, cancellationToken: cancellationToken);

        // An empty page has no memberships to enrich, even when earlier pages contain users.
        if (result.Items.Count == 0)
        {
            return result;
        }

        var ids = result.Items.Select(value => value.Id).ToArray();

        // Resolve memberships in one additional read, not one query per user; the scoped EF context is used sequentially.
        var memberships = await (from membership in authenticationDbContext.UserRoles.AsNoTracking()
                                 join role in authenticationDbContext.Roles on membership.RoleId equals role.Id
                                 where ids.Contains(membership.UserId)
                                 select new { membership.UserId, role.Name }).ToListAsync(cancellationToken);

        // Index the buffered memberships once instead of scanning them again for every user.
        var rolesByUser = memberships.ToLookup(value => value.UserId, value => value.Name!);

        return new(
            result.Items.Select(user => user with
            {
                Roles = rolesByUser[user.Id].ToArray()
            }).ToArray(),
            page,
            result.TotalCount);
    }

    /// <summary>
    /// Reads durable per-session logout state through a fresh database query.
    /// </summary>
    /// <param name="userId">The authenticated account.</param>
    /// <param name="sessionId">The protected session identifier.</param>
    /// <param name="cancellationToken">Cancels the read, including inside a Serializable grant transaction.</param>
    /// <returns>Whether the native token store contains a logout marker.</returns>
    public virtual Task<bool> IsSessionRevokedAsync(Guid userId, string sessionId, CancellationToken cancellationToken) =>
        authenticationDbContext.UserTokens.AsNoTracking().AnyAsync(
            value => value.UserId == userId && value.LoginProvider == AuthenticationInfrastructureConstants.SSO_REVOCATION_PROVIDER &&
                value.Name == sessionId,
            cancellationToken);

    /// <summary>
    /// Gets approved account data with its current role memberships.
    /// </summary>
    /// <param name="id">The account identifier.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The approved account projection.</returns>
    public virtual async Task<UserResponseDto?> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await FirstOrDefaultAsync<UserResponseDto>(predicate: value => value.Id == id, cancellationToken: cancellationToken);

        if (user is null)
        {
            return null;
        }

        var names = await (from membership in authenticationDbContext.UserRoles
                           join role in authenticationDbContext.Roles on membership.RoleId equals role.Id
                           where membership.UserId == id
                           select role.Name!).ToArrayAsync(cancellationToken);

        return user with { Roles = names };
    }

    /// <summary>
    /// Projects native role records into the approved administration contract.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The safe provisioned role metadata.</returns>
    public virtual async Task<IReadOnlyList<RoleResponseDto>> GetRolesAsync(CancellationToken cancellationToken) =>
        await authenticationDbContext.Roles.AsNoTracking().ProjectToType<RoleResponseDto>().ToListAsync(cancellationToken);

    /// <summary>
    /// Filters the current active account and Owner membership in the database before projecting grant metadata.
    /// </summary>
    /// <param name="subject">The authenticated protocol subject.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The minimal permitted account model, or null.</returns>
    public virtual Task<UserAccountModel?> GetPermittedUserAsync(
        string? subject,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(subject, out var id))
        {
            return Task.FromResult<UserAccountModel?>(null);
        }

        // Query current state instead of trusting an old token's Owner claim; no password or stamp is selected.
        return FirstOrDefaultAsync<UserAccountModel>(
            predicate: user => user.Id == id && user.IsActive &&
            authenticationDbContext.UserRoles.Any(membership => membership.UserId == user.Id &&
                authenticationDbContext.Roles.Any(role => role.Id == membership.RoleId && role.Name == AuthenticationConstants.OWNER)),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Locks the invariant row without opening, committing or disposing the caller's transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancels lock acquisition.</param>
    /// <returns>The asynchronous lock acquisition.</returns>
    public virtual async Task LockOwnerMembershipAsync(CancellationToken cancellationToken) =>
        _ = await authenticationDbContext.Roles.FromSqlRaw(AuthenticationSqlConstants.LOCK_OWNER_ROLE, AuthenticationConstants.OWNER.ToUpperInvariant())
            .SingleAsync(cancellationToken);

    /// <summary>
    /// Checks active membership using the inherited native Any query.
    /// </summary>
    /// <param name="excluded">The account being changed.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>Whether another active Owner exists.</returns>
    public virtual Task<bool> HasAnotherActiveOwnerAsync(Guid excluded, CancellationToken cancellationToken) =>
        AnyAsync(
            user => user.Id != excluded && user.IsActive &&
            authenticationDbContext.UserRoles.Any(membership => membership.UserId == user.Id &&
                authenticationDbContext.Roles.Any(role => role.Id == membership.RoleId && role.Name == AuthenticationConstants.OWNER)),
            cancellationToken: cancellationToken);

    /// <summary>
    /// Preserves each key's CLR type while appending the requested direction to an immutable order chain.
    /// </summary>
    /// <typeparam name="TKey">The selected field's CLR type.</typeparam>
    /// <param name="order">The preceding keys, or null for the first key.</param>
    /// <param name="selector">The allowlisted native selector.</param>
    /// <param name="descending">Whether this key is descending.</param>
    /// <returns>The order chain including this key.</returns>
    private static QueryOrder<User> AppendOrder<TKey>(
        QueryOrder<User>? order,
        Expression<Func<User, TKey>> selector,
        bool descending) =>
        order is null ? QueryOrder<User>.By(selector, descending) : order.ThenBy(selector, descending);
}

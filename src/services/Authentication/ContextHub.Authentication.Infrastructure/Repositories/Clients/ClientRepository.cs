namespace ContextHub.Authentication.Infrastructure.Repositories.Clients;

/// <summary>
/// Reuses generic queries and staging while keeping EF entities and atomic persistence behind the feature port.
/// </summary>
/// <param name="authenticationDbContext">Authentication's sole scoped context.</param>
/// <param name="unitOfWork">The shared save boundary for that same context.</param>
/// <param name="logger">The shared repository metadata logger.</param>
public class ClientRepository(
    AuthenticationDbContext authenticationDbContext,
    IUnitOfWork unitOfWork,
    ILogger<ClientRepository> logger)
    : GenericRepository<Client>(authenticationDbContext, logger), IClientRepository
{
    /// <summary>
    /// Gets an approved DTO page through the inherited provider-side Mapster projection.
    /// </summary>
    /// <param name="request">The grouped list inputs.</param>
    /// <param name="cancellationToken">Cancels the sequential database reads.</param>
    /// <returns>The approved application page.</returns>
    public virtual Task<PageResponse<ClientResponseDto>> GetPageAsync(
        GetClientsRequestDto request,
        CancellationToken cancellationToken) =>
        GetPageAsync<ClientResponseDto>(
            new PageRequest(request.Page, request.PageSize),
            QueryOrder<Client>.By(value => value.Name).ThenBy(value => value.Id),
            cancellationToken: cancellationToken);

    /// <summary>
    /// Gets public detail without leaking digests or the internal persistence model.
    /// </summary>
    /// <param name="clientId">The target application.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The approved detail, or null when no application matches.</returns>
    public virtual async Task<ClientDetailResponseDto?> GetDetailAsync(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var client = await FirstOrDefaultAsync<ClientResponseDto>(
            predicate: value => value.Id == clientId,
            cancellationToken: cancellationToken);

        if (client is null)
        {
            return null;
        }

        // Project safe metadata in SQL; digest and raw credential material never belong in this detail response.
        var tokens = await authenticationDbContext.ClientTokens.AsNoTracking().Where(value => value.ClientId == clientId)
            .ProjectToType<ClientTokenResponseDto>().ToArrayAsync(cancellationToken);

        return new(client, tokens);
    }

    /// <summary>
    /// Gets only the internal state the Application needs for its policy decisions.
    /// </summary>
    /// <param name="clientId">The target application.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The model, or null.</returns>
    public virtual Task<ClientModel?> GetAsync(Guid clientId, CancellationToken cancellationToken) =>
        FirstOrDefaultAsync<ClientModel>(predicate: value => value.Id == clientId, cancellationToken: cancellationToken);

    /// <summary>
    /// Keeps credential lookup scoped to the supplied application.
    /// </summary>
    /// <param name="clientId">The owning application.</param>
    /// <param name="tokenId">The target credential.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The internal credential model, or null.</returns>
    public virtual Task<ClientTokenModel?> GetTokenAsync(
        Guid clientId,
        Guid tokenId,
        CancellationToken cancellationToken) =>
        authenticationDbContext.ClientTokens.AsNoTracking().Where(value => value.ClientId == clientId && value.Id == tokenId)
            .ProjectToType<ClientTokenModel>().FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Keeps credential-state and parent-state reads in one provider-side query without loading either tracked entity.
    /// </summary>
    /// <param name="tokenId">The parsed public credential identifier.</param>
    /// <param name="cancellationToken">Cancels the database query.</param>
    /// <returns>The internal digest and metadata, or null when current database state forbids access.</returns>
    public virtual Task<ClientTokenModel?> GetValidTokenAsync(Guid tokenId, CancellationToken cancellationToken) =>
        authenticationDbContext.ClientTokens.AsNoTracking()
            // Null expiry is unlimited; revocation and parent status still apply on every lookup.
            .Where(value => value.Id == tokenId && value.RevokedAt == null && value.Client.IsActive &&
                (value.ExpiresAt == null || value.ExpiresAt > DateTimeOffset.UtcNow))
            .ProjectToType<ClientTokenModel>().FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Stages both records and saves once; native EF makes this multi-record write atomic.
    /// </summary>
    /// <param name="client">The application state approved by the handler.</param>
    /// <param name="token">Its first credential metadata and digest.</param>
    /// <param name="cancellationToken">Cancels staging and the final save.</param>
    /// <returns>The completed persistence operation.</returns>
    public virtual async Task CreateAsync(
        ClientModel client,
        ClientTokenModel token,
        CancellationToken cancellationToken)
    {
        // Both entries share one context and save; the interceptor supplies audit values for the same write boundary.
        await AddAsync(client.Adapt<Client>(), cancellationToken);
        await authenticationDbContext.ClientTokens.AddAsync(token.Adapt<ClientToken>(), cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Applies an approved model using its native concurrency value without replacing creation metadata.
    /// </summary>
    /// <param name="client">The approved state and opaque expected version.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The saved projection with its refreshed concurrency value.</returns>
    public virtual async Task<ClientResponseDto> UpdateAsync(ClientModel client, CancellationToken cancellationToken)
    {
        var entity = client.Adapt<Client>();
        Update(entity);

        // Creation metadata is not editable; the opaque model version becomes EF's concurrency predicate.
        authenticationDbContext.Entry(entity).Property(value => value.CreatedAt).IsModified = false;
        authenticationDbContext.Entry(entity).Property(value => value.CreatedBy).IsModified = false;

        try
        {
            // The handler's version check cannot serialize two requests. Let the database enforce the original xmin too.
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception) when (
            exception.Entries.Count == 1 && ReferenceEquals(exception.Entries[0].Entity, entity))
        {
            // Classify only an optimistic write conflict; provider/configuration errors retain their native failure path.
            logger.LogWarning(AuthenticationLogConstants.OPERATION_REJECTED, nameof(UpdateAsync),
                AuthenticationErrorConstants.CONCURRENCY_CONFLICT);
            throw new BusinessException(AuthenticationErrorConstants.CONCURRENCY_CONFLICT,
                AuthenticationErrorConstants.ENTITY_CHANGED, BusinessErrorKind.Conflict);
        }

        return entity.Adapt<ClientResponseDto>();
    }

    /// <summary>
    /// Persists credential material prepared by the Application issuance policy.
    /// </summary>
    /// <param name="token">The approved metadata and digest.</param>
    /// <param name="cancellationToken">Cancels staging and save.</param>
    /// <returns>The completed persistence operation.</returns>
    public virtual async Task AddTokenAsync(ClientTokenModel token, CancellationToken cancellationToken)
    {
        await authenticationDbContext.ClientTokens.AddAsync(token.Adapt<ClientToken>(), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Updates only credential lifecycle fields, never replacing digest or issuance metadata.
    /// </summary>
    /// <param name="token">The lifecycle state approved by the handler.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The safe projection after shared audit stamping.</returns>
    public virtual async Task<ClientTokenResponseDto> UpdateTokenAsync(
        ClientTokenModel token,
        CancellationToken cancellationToken)
    {
        var entity = token.Adapt<ClientToken>();
        authenticationDbContext.Attach(entity);

        // Mark only lifecycle columns; the model is not permission to overwrite the stored digest.
        authenticationDbContext.Entry(entity).Property(value => value.RevokedAt).IsModified = true;
        authenticationDbContext.Entry(entity).Property(value => value.RevokedBy).IsModified = true;

        // Every first revocation expects the previously read value to remain null.
        authenticationDbContext.Entry(entity).Property(value => value.RevokedAt).OriginalValue = null;

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception) when (
            exception.Entries.Count == 1 && ReferenceEquals(exception.Entries[0].Entity, entity))
        {
            // A competing revoke owns the original actor/time; read its committed result instead of overwriting it.
            authenticationDbContext.Entry(entity).State = EntityState.Detached;
            var committed = await GetTokenAsync(token.ClientId, token.TokenId, cancellationToken);
            if (committed?.RevokedAt is not null)
            {
                return committed.Adapt<ClientTokenResponseDto>();
            }

            logger.LogWarning(AuthenticationLogConstants.OPERATION_REJECTED, nameof(UpdateTokenAsync),
                AuthenticationErrorConstants.CONCURRENCY_CONFLICT);
            throw new BusinessException(AuthenticationErrorConstants.CONCURRENCY_CONFLICT,
                AuthenticationErrorConstants.ENTITY_CHANGED, BusinessErrorKind.Conflict);
        }

        // Read database precision so the first response and later idempotent responses carry the same timestamp.
        return await authenticationDbContext.ClientTokens.AsNoTracking()
            .Where(value => value.ClientId == token.ClientId && value.Id == token.TokenId)
            .ProjectToType<ClientTokenResponseDto>().SingleAsync(cancellationToken);
    }
}

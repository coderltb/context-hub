namespace ContextHub.Authentication.Infrastructure.Services.Clients;

/// <summary>
/// Keeps client credential configuration, current-state IO and native helper composition behind the feature port.
/// </summary>
public sealed class ClientService : IClientService
{
    /// <summary>
    /// Owns client queries and persistence without exposing entities to Application.
    /// </summary>
    private readonly IClientRepository _clientRepository;

    /// <summary>
    /// Supplies the authenticated actor for explicit credential lifecycle metadata.
    /// </summary>
    private readonly IAuditActorAccessor _auditActorAccessor;

    /// <summary>
    /// Holds only the externally supplied service key; raw issued credentials are never retained here.
    /// </summary>
    private readonly byte[] _clientTokenPepper;

    /// <summary>
    /// Binds IO dependencies and validates key material once at this service's cryptographic boundary.
    /// </summary>
    /// <param name="clientRepository">The same feature persistence boundary used by account operations.</param>
    /// <param name="authenticationOptions">The externally supplied client-token key configuration.</param>
    /// <param name="auditActorAccessor">The current actor without a request-body override.</param>
    /// <param name="logger">The configuration-rejection logger, never a key or credential logger.</param>
    public ClientService(
        IClientRepository clientRepository,
        IOptions<AuthenticationOptions> authenticationOptions,
        IAuditActorAccessor auditActorAccessor,
        ILogger<ClientService> logger)
    {
        _clientRepository = clientRepository;
        _auditActorAccessor = auditActorAccessor;
        _clientTokenPepper = Convert.FromBase64String(authenticationOptions.Value.ClientTokenPepper);

        // Native HMAC accepts an empty key; this required service key must not become publicly reproducible material.
        if (_clientTokenPepper.Length < AuthenticationInfrastructureConstants.MIN_CLIENT_TOKEN_KEY_BYTES)
        {
            logger.LogError(
                AuthenticationLogConstants.OPERATION_REJECTED,
                nameof(ClientService),
                AuthenticationErrorConstants.INVALID_CLIENT_TOKEN_KEY);
            throw new CryptographicException(AuthenticationErrorConstants.INVALID_CLIENT_TOKEN_KEY);
        }
    }

    /// <summary>
    /// Gets approved paginated metadata without exposing the entity store.
    /// </summary>
    /// <param name="request">The grouped list input.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The approved page.</returns>
    public Task<PageResponse<ClientResponseDto>> GetPageAsync(
        GetClientsRequestDto request,
        CancellationToken cancellationToken) =>
        _clientRepository.GetPageAsync(request, cancellationToken);

    /// <summary>
    /// Gets approved client and credential metadata.
    /// </summary>
    /// <param name="clientId">The target application.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The safe detail, or null.</returns>
    public Task<ClientDetailResponseDto?> GetDetailAsync(Guid clientId, CancellationToken cancellationToken) =>
        _clientRepository.GetDetailAsync(clientId, cancellationToken);

    /// <summary>
    /// Gets internal state used by Application workflow decisions.
    /// </summary>
    /// <param name="clientId">The target application.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The Application model, or null.</returns>
    public Task<ClientModel?> GetAsync(Guid clientId, CancellationToken cancellationToken) =>
        _clientRepository.GetAsync(clientId, cancellationToken);

    /// <summary>
    /// Gets credential state within its owning application.
    /// </summary>
    /// <param name="clientId">The owning application.</param>
    /// <param name="tokenId">The target credential.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The lifecycle model, or null.</returns>
    public Task<ClientTokenModel?> GetTokenAsync(
        Guid clientId,
        Guid tokenId,
        CancellationToken cancellationToken) =>
        _clientRepository.GetTokenAsync(clientId, tokenId, cancellationToken);

    /// <summary>
    /// Checks format, current persisted state and the complete HMAC before granting native introspection an identity.
    /// </summary>
    /// <param name="token">The untrusted bearer value, never logged or accepted from another scheme.</param>
    /// <param name="cancellationToken">Cancels the current-state query.</param>
    /// <returns>The verified current credential state, or null without account-specific diagnostics.</returns>
    public async Task<ClientTokenModel?> ValidateTokenAsync(
        string? token,
        CancellationToken cancellationToken)
    {
        // Reject malformed credentials before database I/O; the embedded identifier is only a lookup hint.
        if (!ClientTokenHelper.TryGetId(token, out var tokenId))
        {
            return null;
        }

        // The identifier alone grants nothing; do not cache either live client status or the digest decision.
        var stored = await _clientRepository.GetValidTokenAsync(tokenId, cancellationToken);

        // Verify the complete bearer value against its HMAC digest; metadata or a matching identifier alone grants no access.
        return stored is not null && ClientTokenHelper.Verify(token, stored.SecretHash, _clientTokenPepper)
            ? stored : null;
    }

    /// <summary>
    /// Persists the application and first credential together; audit values are supplied at SaveChanges.
    /// </summary>
    /// <param name="client">The approved application state.</param>
    /// <param name="request">The grouped tool grants and optional expiration.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The one-time raw response.</returns>
    public async Task<IssuedClientTokenResponseDto> CreateAsync(
        ClientModel client,
        IssueClientTokenRequestDto request,
        CancellationToken cancellationToken)
    {
        // Generate credential material once for this operation; only the approved tool grant set is copied into it.
        var issued = CreateCredential(client.ClientId, request);

        // Persist only the digest model; expose the raw response only after the repository's save succeeds.
        await _clientRepository.CreateAsync(client, issued.Adapt<ClientTokenModel>(), cancellationToken);

        return issued.Adapt<IssuedClientTokenResponseDto>();
    }

    /// <summary>
    /// Saves metadata through the same feature repository without duplicating concurrency handling.
    /// </summary>
    /// <param name="client">The approved metadata and expected version.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The saved response, including its refreshed concurrency value.</returns>
    public Task<ClientResponseDto> UpdateAsync(ClientModel client, CancellationToken cancellationToken) =>
        _clientRepository.UpdateAsync(client, cancellationToken);

    /// <summary>
    /// Adds a new credential without refreshing or revoking any other application token.
    /// </summary>
    /// <param name="client">The application approved by the handler.</param>
    /// <param name="request">The grouped tool grants and optional expiration.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The one-time raw response.</returns>
    public async Task<IssuedClientTokenResponseDto> IssueAsync(
        ClientModel client,
        IssueClientTokenRequestDto request,
        CancellationToken cancellationToken)
    {
        // Issue an independent credential for rotation or a different grant set; the existing tokens remain separate records.
        var issued = CreateCredential(client.ClientId, request);

        // Adding one credential must not update another token's grants, lifetime or revocation state.
        await _clientRepository.AddTokenAsync(issued.Adapt<ClientTokenModel>(), cancellationToken);

        return issued.Adapt<IssuedClientTokenResponseDto>();
    }

    /// <summary>
    /// Applies revocation once; audit timestamps remain owned by the interceptor, not the handler.
    /// </summary>
    /// <param name="token">The credential approved by the handler.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The safe lifecycle result.</returns>
    public Task<ClientTokenResponseDto> RevokeAsync(ClientTokenModel token, CancellationToken cancellationToken) =>
        token.RevokedAt is not null
            ? Task.FromResult(token.Adapt<ClientTokenResponseDto>())
            : _clientRepository.UpdateTokenAsync(
                token with {
                    RevokedAt = DateTimeOffset.UtcNow,
                    RevokedBy = _auditActorAccessor.ActorId
                },
                cancellationToken);

    /// <summary>
    /// Generates credential material before the mapping profile separates persistence data from the response.
    /// </summary>
    /// <param name="clientId">The receiving application.</param>
    /// <param name="request">The grant set and optional expiry, copied before persistence.</param>
    /// <returns>The transient generated material, never persisted or logged as a whole.</returns>
    private ClientTokenIssuanceModel CreateCredential(
        Guid clientId,
        IssueClientTokenRequestDto request)
    {
        // Create a public metadata identifier and fresh random secret; only the keyed digest is persisted.
        var id = Guid.CreateVersion7();
        var material = ClientTokenHelper.Issue(id, _clientTokenPepper);

        // Snapshot the caller's list before persistence; later list mutations cannot change the issued grants.
        var grants = request.AllowedTools.Distinct(StringComparer.Ordinal).ToArray();

        // Mapping separates the persisted digest from the one-time token response.
        return new(
            clientId,
            id,
            AuthenticationConstants.CLIENT_TOKEN_PREFIX + id.ToString("N"),
            material.Token,
            material.Digest,
            grants,
            request.ExpiresAt?.ToUniversalTime());
    }
}

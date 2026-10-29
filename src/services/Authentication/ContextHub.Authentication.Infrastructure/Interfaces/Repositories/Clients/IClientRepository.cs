namespace ContextHub.Authentication.Infrastructure.Interfaces.Repositories.Clients;

/// <summary>
/// Exposes persistence through DTOs and models; EF entities and UoW remain in Infrastructure.
/// </summary>
public interface IClientRepository
{
    /// <summary>
    /// Gets the approved client page.
    /// </summary>
    /// <param name="request">The grouped list inputs.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The approved page.</returns>
    Task<PageResponse<ClientResponseDto>> GetPageAsync(
        GetClientsRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets safe application and credential metadata.
    /// </summary>
    /// <param name="clientId">The target application.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The safe detail, or null.</returns>
    Task<ClientDetailResponseDto?> GetDetailAsync(Guid clientId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets state needed for Application decisions.
    /// </summary>
    /// <param name="clientId">The target application.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The Application model, or null.</returns>
    Task<ClientModel?> GetAsync(Guid clientId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets one credential within its owning application.
    /// </summary>
    /// <param name="clientId">The owning application.</param>
    /// <param name="tokenId">The target credential.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The internal credential state, or null.</returns>
    Task<ClientTokenModel?> GetTokenAsync(
        Guid clientId,
        Guid tokenId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets an unrevoked, unexpired credential whose owning client remains active.
    /// </summary>
    /// <param name="tokenId">The public identifier parsed from the opaque credential.</param>
    /// <param name="cancellationToken">Cancels the bounded lookup.</param>
    /// <returns>The internal credential material to verify, or null when its current state disallows access.</returns>
    Task<ClientTokenModel?> GetValidTokenAsync(Guid tokenId, CancellationToken cancellationToken);

    /// <summary>
    /// Persists an application and its first credential atomically.
    /// </summary>
    /// <param name="client">The approved application state.</param>
    /// <param name="token">The first credential metadata and digest.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The completed persistence operation.</returns>
    Task CreateAsync(
        ClientModel client,
        ClientTokenModel token,
        CancellationToken cancellationToken);

    /// <summary>
    /// Persists approved metadata using the opaque expected version.
    /// </summary>
    /// <param name="client">The approved state and expected version.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The saved response with the updated native concurrency value.</returns>
    Task<ClientResponseDto> UpdateAsync(ClientModel client, CancellationToken cancellationToken);

    /// <summary>
    /// Persists an explicitly issued credential.
    /// </summary>
    /// <param name="token">The metadata and digest approved by the use case.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The completed persistence operation.</returns>
    Task AddTokenAsync(ClientTokenModel token, CancellationToken cancellationToken);

    /// <summary>
    /// Persists an approved credential lifecycle change.
    /// </summary>
    /// <param name="token">The lifecycle state approved by the use case.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The safe metadata after audit stamping and persistence.</returns>
    Task<ClientTokenResponseDto> UpdateTokenAsync(ClientTokenModel token, CancellationToken cancellationToken);
}

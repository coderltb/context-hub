namespace ContextHub.Authentication.Application.Interfaces.Clients;

/// <summary>
/// Keeps client persistence and credential generation behind an Infrastructure service without exposing entities or repositories.
/// </summary>
public interface IClientService
{
    /// <summary>
    /// Gets the approved application page.
    /// </summary>
    /// <param name="request">The grouped list input.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The safe application page.</returns>
    Task<PageResponse<ClientResponseDto>> GetPageAsync(
        GetClientsRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets safe client and credential metadata.
    /// </summary>
    /// <param name="clientId">The target application.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The approved detail, or null.</returns>
    Task<ClientDetailResponseDto?> GetDetailAsync(Guid clientId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the Application model required by use-case state decisions.
    /// </summary>
    /// <param name="clientId">The target application.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The application state, or null.</returns>
    Task<ClientModel?> GetAsync(Guid clientId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a credential within its owning application.
    /// </summary>
    /// <param name="clientId">The owning application.</param>
    /// <param name="tokenId">The credential identifier.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The internal lifecycle state, or null.</returns>
    Task<ClientTokenModel?> GetTokenAsync(
        Guid clientId,
        Guid tokenId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Authenticates a client credential against its supported format, current state and complete stored digest.
    /// </summary>
    /// <param name="token">The untrusted bearer value, never logged.</param>
    /// <param name="cancellationToken">Cancels the bounded current-state read.</param>
    /// <returns>The verified internal state, or null for every unavailable credential.</returns>
    Task<ClientTokenModel?> ValidateTokenAsync(string? token, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the approved application and its first token in one persistence boundary.
    /// </summary>
    /// <param name="client">The business state prepared by the handler.</param>
    /// <param name="request">The grouped tool grants and optional expiry.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The raw credential returned only by issuance.</returns>
    Task<IssuedClientTokenResponseDto> CreateAsync(
        ClientModel client,
        IssueClientTokenRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Saves approved metadata using its expected native version.
    /// </summary>
    /// <param name="client">The desired state and expected version.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The saved safe response.</returns>
    Task<ClientResponseDto> UpdateAsync(ClientModel client, CancellationToken cancellationToken);

    /// <summary>
    /// Issues an independent credential without changing existing credentials.
    /// </summary>
    /// <param name="client">The application already approved by the handler.</param>
    /// <param name="request">The grouped tool grants and optional expiry.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The one-time raw response.</returns>
    Task<IssuedClientTokenResponseDto> IssueAsync(
        ClientModel client,
        IssueClientTokenRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Revokes an approved credential while preserving the original revocation on repeated calls.
    /// </summary>
    /// <param name="token">The state loaded by the handler.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The safe credential metadata.</returns>
    Task<ClientTokenResponseDto> RevokeAsync(ClientTokenModel token, CancellationToken cancellationToken);
}

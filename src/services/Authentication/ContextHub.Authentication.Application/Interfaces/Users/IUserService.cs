namespace ContextHub.Authentication.Application.Interfaces.Users;

/// <summary>
/// Provides account use cases over native Identity while hiding framework managers from transport.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Gets the approved account page with explicit search and ordering.
    /// </summary>
    /// <param name="request">The grouped pagination, filter, keyword and ordering inputs.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The account page and current memberships.</returns>
    Task<PageResponse<UserResponseDto>> GetPageAsync(GetUsersRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Gets one approved account projection.
    /// </summary>
    /// <param name="id">The target account.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The safe account projection.</returns>
    Task<UserResponseDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Changes the current Owner's password and revokes its human grants atomically.
    /// </summary>
    /// <param name="id">The subject supplied by the authenticated Application handler.</param>
    /// <param name="request">Current credential proof and the replacement password.</param>
    /// <param name="cancellationToken">Cancels the owned database work.</param>
    /// <returns>The approved profile after the credential change commits.</returns>
    Task<UserResponseDto> ChangePasswordAsync(
        Guid id,
        ChangePasswordRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates an Owner using native password policy.
    /// </summary>
    /// <param name="request">The account credentials; role selection stays server-owned.</param>
    /// <param name="cancellationToken">Cancels provisioning.</param>
    /// <returns>The approved account projection.</returns>
    Task<UserResponseDto> CreateAsync(CreateUserRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a passwordless account and a purpose-bound native invitation.
    /// </summary>
    /// <param name="request">The explicit invitation metadata and role selection.</param>
    /// <param name="cancellationToken">Cancels the owned transaction.</param>
    /// <returns>The account and one-time setup URL.</returns>
    Task<InvitedUserResponseDto> InviteAsync(InviteUserRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Changes account status under the last-active-Owner invariant.
    /// </summary>
    /// <param name="id">The target account.</param>
    /// <param name="request">The desired status and expected version.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The latest account projection.</returns>
    Task<UserResponseDto> SetStatusAsync(
        Guid id,
        UserStatusRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces memberships and invalidates affected authorization state.
    /// </summary>
    /// <param name="id">The target account.</param>
    /// <param name="request">The desired memberships and expected version.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The latest account projection.</returns>
    Task<UserResponseDto> ReplaceRolesAsync(
        Guid id,
        UserRolesRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces a pending invitation without exposing a password-reset API.
    /// </summary>
    /// <param name="id">The account awaiting enrollment.</param>
    /// <param name="request">The expected account version.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The replacement one-time invitation.</returns>
    Task<InvitedUserResponseDto> RegenerateInvitationAsync(
        Guid id,
        InvitationRequestDto request,
        CancellationToken cancellationToken);
}

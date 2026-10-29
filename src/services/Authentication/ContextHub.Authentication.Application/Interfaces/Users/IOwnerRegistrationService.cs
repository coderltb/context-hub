namespace ContextHub.Authentication.Application.Interfaces.Users;

/// <summary>
/// Exposes the account registration workflow without persistence entities or native managers.
/// </summary>
public interface IOwnerRegistrationService
{
    /// <summary>
    /// Creates an Owner with native registrations and account membership committed together.
    /// </summary>
    /// <param name="request">The submitted username, recovery email and password.</param>
    /// <param name="cancellationToken">Cancels native provisioning.</param>
    /// <returns>The committed Owner projection without credentials.</returns>
    Task<UserResponseDto> RegisterAsync(
        CreateUserRequestDto request,
        CancellationToken cancellationToken);
}

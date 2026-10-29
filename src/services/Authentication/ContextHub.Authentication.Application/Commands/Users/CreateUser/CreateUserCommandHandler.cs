namespace ContextHub.Authentication.Application.Commands.Users.CreateUser;

/// <summary>
/// Orchestrates the public account registration through the Application-owned port.
/// </summary>
/// <param name="ownerRegistrationService">The account boundary implemented using native Identity managers.</param>
public sealed class CreateUserCommandHandler(IOwnerRegistrationService ownerRegistrationService)
    : IRequestHandler<CreateUserCommand, ApiResponse<UserResponseDto>>
{
    /// <summary>
    /// Runs the operation after the shared validation behavior.
    /// </summary>
    /// <param name="request">The typed use case input.</param>
    /// <param name="cancellationToken">Cancels account reads or writes.</param>
    /// <returns>The approved account response.</returns>
    public async ValueTask<ApiResponse<UserResponseDto>> Handle(
        CreateUserCommand request,
        CancellationToken cancellationToken)
    {
        // The account service creates the native user and role atomically; the password never enters diagnostics.
        var result = await ownerRegistrationService.RegisterAsync(request.Request, cancellationToken);

        return new(result);
    }
}

namespace ContextHub.Authentication.Application.Commands.Users.RegenerateInvitation;

/// <summary>
/// Orchestrates the authorized account operation through the Application-owned port.
/// </summary>
/// <param name="userService">The account boundary implemented using native Identity managers.</param>
public sealed class RegenerateInvitationCommandHandler(IUserService userService)
    : IRequestHandler<RegenerateInvitationCommand, ApiResponse<InvitationResponseDto>>
{
    /// <summary>
    /// Runs the operation after the shared authorization and validation behaviors.
    /// </summary>
    /// <param name="request">The typed use case input.</param>
    /// <param name="cancellationToken">Cancels account reads or writes.</param>
    /// <returns>The approved account response.</returns>
    public async ValueTask<ApiResponse<InvitationResponseDto>> Handle(
        RegenerateInvitationCommand request,
        CancellationToken cancellationToken)
    {
        // Return only the replacement invitation contract; neither version material nor the one-time link belongs in diagnostics.
        var result = await userService.RegenerateInvitationAsync(
            request.UserId,
            request.Request,
            cancellationToken);

        return new(result.Adapt<InvitationResponseDto>());
    }
}

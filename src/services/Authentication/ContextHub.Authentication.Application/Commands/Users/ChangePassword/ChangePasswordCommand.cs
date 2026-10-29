namespace ContextHub.Authentication.Application.Commands.Users.ChangePassword;

/// <summary>
/// Changes only the authenticated Owner's password after the shared authorization and validation behaviors.
/// </summary>
/// <param name="Request">The current credential proof and replacement password.</param>
public sealed record ChangePasswordCommand(ChangePasswordRequestDto Request)
    : IRequest<ApiResponse<UserResponseDto>>, IAdminRequest;

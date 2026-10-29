namespace ContextHub.Authentication.Application.Commands.Users.InviteUser;

/// <summary>
/// Defines the create user use case independently of HTTP and native Identity.
/// </summary>
/// <param name="Request">The submitted account metadata.</param>
public sealed record InviteUserCommand(InviteUserRequestDto Request) : IRequest<ApiResponse<InvitedUserResponseDto>>, IAdminRequest;

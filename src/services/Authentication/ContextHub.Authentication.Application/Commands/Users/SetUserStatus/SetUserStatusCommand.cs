namespace ContextHub.Authentication.Application.Commands.Users.SetUserStatus;

/// <summary>
/// Defines the set user status use case independently of HTTP and native Identity.
/// </summary>
/// <param name="UserId">The target account identifier.</param>
/// <param name="Request">The submitted account metadata.</param>
public sealed record SetUserStatusCommand(Guid UserId, UserStatusRequestDto Request) : IRequest<ApiResponse<UserResponseDto>>, IAdminRequest;

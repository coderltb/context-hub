namespace ContextHub.Authentication.Application.Commands.Users.CreateUser;

/// <summary>
/// Defines the create user use case independently of HTTP and native Identity.
/// </summary>
/// <param name="Request">The submitted account metadata.</param>
public sealed record CreateUserCommand(CreateUserRequestDto Request) : IRequest<ApiResponse<UserResponseDto>>;

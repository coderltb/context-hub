namespace ContextHub.Authentication.Application.Commands.Roles.ReplaceUserRoles;

/// <summary>
/// Defines the replace user roles use case independently of HTTP and native Identity.
/// </summary>
/// <param name="UserId">The target account identifier.</param>
/// <param name="Request">The submitted account metadata.</param>
public sealed record ReplaceUserRolesCommand(Guid UserId, UserRolesRequestDto Request) : IRequest<ApiResponse<UserResponseDto>>, IAdminRequest;

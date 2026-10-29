namespace ContextHub.Authentication.Application.Queries.Roles.GetRoles;

/// <summary>
/// Defines the get roles use case independently of HTTP and native Identity.
/// </summary>
public sealed record GetRolesQuery : IRequest<ApiResponse<RoleListResponseDto>>, IAdminRequest;

namespace ContextHub.Authentication.Application.Queries.Users.GetUsers;

/// <summary>
/// Defines the get users use case independently of HTTP and native Identity.
/// </summary>
/// <param name="Request">The grouped list inputs.</param>
public sealed record GetUsersQuery(GetUsersRequestDto Request) : IRequest<ApiResponse<PageResponse<UserResponseDto>>>, IAdminRequest;

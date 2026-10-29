namespace ContextHub.Authentication.Application.Dtos.Roles;

/// <summary>
/// Preserves the role-list contract without putting anonymous response construction in the HTTP adapter.
/// </summary>
/// <param name="Items">The provisioned role metadata.</param>
[PublicAPI]
public sealed record RoleListResponseDto(IReadOnlyList<RoleResponseDto> Items);

namespace ContextHub.Authentication.Application.Queries.Roles.GetRoles;

/// <summary>
/// Orchestrates the authorized account operation through the Application-owned port.
/// </summary>
/// <param name="roleService">The account boundary implemented using native Identity managers.</param>
public sealed class GetRolesQueryHandler(IRoleService roleService)
    : IRequestHandler<GetRolesQuery, ApiResponse<RoleListResponseDto>>
{
    /// <summary>
    /// Runs the operation after the shared authorization and validation behaviors.
    /// </summary>
    /// <param name="request">The typed use case input.</param>
    /// <param name="cancellationToken">Cancels account reads or writes.</param>
    /// <returns>The approved account response.</returns>
    public async ValueTask<ApiResponse<RoleListResponseDto>> Handle(
        GetRolesQuery request,
        CancellationToken cancellationToken)
    {
        // Return only provisioned role projections; framework claims and membership records do not cross this boundary.
        var result = await roleService.GetRolesAsync(cancellationToken);

        return new(new RoleListResponseDto(result));
    }
}

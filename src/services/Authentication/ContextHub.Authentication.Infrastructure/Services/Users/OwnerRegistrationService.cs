namespace ContextHub.Authentication.Infrastructure.Services.Users;

/// <summary>
/// Registers Owner accounts through the existing native provisioning and account boundaries.
/// </summary>
/// <param name="identityProvisioningService">The native role and protocol registration boundary.</param>
/// <param name="userService">The native account creation port sharing this transaction.</param>
/// <param name="roleService">The display-metadata cache invalidated after provisioning commits.</param>
/// <param name="unitOfWork">The service's shared scoped transaction owner.</param>
public sealed class OwnerRegistrationService(
    IIdentityProvisioningService identityProvisioningService,
    IUserService userService,
    IRoleService roleService,
    IUnitOfWork unitOfWork) : IOwnerRegistrationService
{
    /// <summary>
    /// Creates an Owner without treating existing accounts as a registration limit.
    /// </summary>
    /// <param name="request">The submitted credentials consumed only by native Identity.</param>
    /// <param name="cancellationToken">Cancels provisioning and account persistence.</param>
    /// <returns>The committed profile without credentials.</returns>
    public async Task<UserResponseDto> RegisterAsync(
        CreateUserRequestDto request,
        CancellationToken cancellationToken)
    {
        // Provision required native records before the account workflow takes its membership lock.
        var created = await unitOfWork.ExecuteInTransactionAsync(
            async token =>
        {
            await identityProvisioningService.ProvisionAsync(token);

            return await userService.CreateAsync(request, token);
        },
            cancellationToken);

        // Display metadata is invalidated only after the native records commit.
        await roleService.InvalidateCacheAsync(cancellationToken);

        return created;
    }
}

namespace ContextHub.Authentication.Infrastructure.Mappings.Identity;

/// <summary>
/// Defines approved response projections once for the host's global Mapster registration.
/// </summary>
public sealed class IdentityMapping : IRegister
{
    /// <summary>
    /// Excludes credentials by mapping into explicit DTOs rather than serializing framework entities.
    /// </summary>
    /// <param name="config">The startup-owned global configuration.</param>
    public void Register(TypeAdapterConfig config)
    {
        // Provisioning copies only approved metadata; generated identity and credential fields stay server-owned.
        config.NewConfig<InviteUserRequestDto, User>()
            .Map(user => user.UserName, request => request.UserName.Trim())
            .Map(user => user.Email, request => request.Email.Trim())
            .Map(user => user.IsActive, request => request.IsActive)
            .IgnoreNonMapped(true);

        // Credential DTOs cannot populate framework hashes, keys or stamps through convention mapping.
        config.NewConfig<CreateUserRequestDto, User>()
            .Map(user => user.UserName, request => request.UserName.Trim())
            .Map(user => user.Email, request => request.Email.Trim())
            .Map(user => user.IsActive, _ => true)
            .IgnoreNonMapped(true);

        config.NewConfig<User, UserAccountModel>();

        config.NewConfig<IdentityRole<Guid>, RoleResponseDto>()
            .Map(value => value.Description, _ => AuthenticationConstants.OWNER_ROLE_DESCRIPTION);

        config.NewConfig<User, UserResponseDto>()
            .Map(value => value.Roles, _ => Array.Empty<string>())
            .Map(value => value.RoleIds, _ => Array.Empty<Guid>())
            .Map(value => value.ConcurrencyToken, value => value.ConcurrencyStamp ?? "")
            .Map(value => value.UserName, value => value.UserName ?? "");
    }
}

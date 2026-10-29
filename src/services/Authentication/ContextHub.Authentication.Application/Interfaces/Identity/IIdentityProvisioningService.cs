namespace ContextHub.Authentication.Application.Interfaces.Identity;

/// <summary>
/// Keeps explicit Identity/OpenIddict provisioning behind an Application-owned port.
/// </summary>
public interface IIdentityProvisioningService
{
    /// <summary>
    /// Creates missing roles and protocol registrations without replacing existing metadata.
    /// </summary>
    /// <param name="cancellationToken">Cancels native manager operations.</param>
    /// <returns>The provisioning operation without creating an account or starting a host job.</returns>
    Task ProvisionAsync(CancellationToken cancellationToken);
}

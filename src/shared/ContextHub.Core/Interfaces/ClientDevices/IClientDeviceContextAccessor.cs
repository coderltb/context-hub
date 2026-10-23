namespace ContextHub.Core.Interfaces.ClientDevices;

/// <summary>
/// Reads optional client metadata without authenticating the caller or restricting client eligibility.
/// </summary>
public interface IClientDeviceContextAccessor
{
    /// <summary>
    /// Reads the current execution context without retaining a previous request's metadata.
    /// </summary>
    /// <returns>A snapshot whose unavailable fields remain null.</returns>
    ClientDeviceContextModel GetCurrent();
}

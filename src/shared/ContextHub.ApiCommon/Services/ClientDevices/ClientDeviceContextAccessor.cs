namespace ContextHub.ApiCommon.Services.ClientDevices;

/// <summary>
/// Reads optional headers from the ambient HTTP request without enforcing device or authentication policy.
/// </summary>
/// <param name="httpContextAccessor">The host-owned ambient request accessor.</param>
public sealed class ClientDeviceContextAccessor(IHttpContextAccessor httpContextAccessor) : IClientDeviceContextAccessor
{
    /// <summary>
    /// Creates an immutable snapshot without fabricated defaults or database-specific length limits.
    /// </summary>
    /// <returns>Current metadata, or null field values when no request/header exists.</returns>
    public ClientDeviceContextModel GetCurrent()
    {
        var context = httpContextAccessor.HttpContext;
        var headers = context?.Request.Headers;

        // Headers remain self-declared metadata. Missing values do not make a client ineligible.
        return new ClientDeviceContextModel
        {
            DeviceId = headers?[ApiCommonConstants.ClientDeviceHeaders.DEVICE_ID].ToString().NormalizeOptional(),
            DeviceName = headers?[ApiCommonConstants.ClientDeviceHeaders.DEVICE_NAME].ToString().NormalizeOptional(),
            DeviceType = headers?[ApiCommonConstants.ClientDeviceHeaders.DEVICE_TYPE].ToString().NormalizeOptional(),
            UserAgent = headers?.UserAgent.ToString().NormalizeOptional(),
            IpAddress = context?.Connection.RemoteIpAddress?.ToString()
        };
    }
}

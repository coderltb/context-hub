namespace ContextHub.Core.Models.ClientDevices;

/// <summary>
/// Carries untrusted optional device metadata, not evidence of identity or authorization.
/// </summary>
public sealed record ClientDeviceContextModel
{
    /// <summary>
    /// Gets the caller-declared device identifier, when supplied.
    /// </summary>
    public string? DeviceId { get; init; }

    /// <summary>
    /// Gets the caller-declared device name without service-specific storage truncation.
    /// </summary>
    public string? DeviceName { get; init; }

    /// <summary>
    /// Gets the optional caller-declared device category.
    /// </summary>
    public string? DeviceType { get; init; }

    /// <summary>
    /// Gets the optional user-agent text; it must not determine eligibility.
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// Gets the connection address after any host-owned proxy normalization.
    /// </summary>
    public string? IpAddress { get; init; }
}

namespace ContextHub.Backend.Tests.Shared.ContextHub.ApiCommon.DependencyInjection;

/// <summary>
/// Verifies that logging can obtain correlation dependencies without activating Admin response adapters.
/// </summary>
public sealed class CorrelationRegistrationTests
{
    /// <summary>
    /// Keeps a host-owned clock and leaves the transport response factories unregistered.
    /// </summary>
    [Fact]
    public void CorrelationRegistration_PreservesClockWithoutAdminAdapters()
    {
        var services = new ServiceCollection();
        var clock = new HostClock();
        services.AddSingleton<TimeProvider>(clock);
        services.AddCorrelation();
        services.AddCorrelation();
        using var provider = services.BuildServiceProvider();

        Assert.Same(clock, provider.GetRequiredService<TimeProvider>());
        Assert.Null(provider.GetService<ApiErrorResponseFactory>());
        Assert.Null(provider.GetService<ApiResponseMetadataFactory>());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(TimeProvider));
    }

    /// <summary>
    /// Supplies the default clock when a host registers only the correlation seam.
    /// </summary>
    [Fact]
    public void CorrelationRegistration_SuppliesDefaultClock()
    {
        var services = new ServiceCollection();
        services.AddCorrelation();
        using var provider = services.BuildServiceProvider();

        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }

    /// <summary>
    /// Supplies a distinct host clock so replacing an override with the system clock cannot pass unnoticed.
    /// </summary>
    private sealed class HostClock : TimeProvider
    {
    }
}

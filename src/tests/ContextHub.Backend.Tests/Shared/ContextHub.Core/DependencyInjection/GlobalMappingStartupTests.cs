namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.DependencyInjection;

/// <summary>
/// Verifies native Adapt without resolving a mapper or configuration.
/// </summary>
[Collection(nameof(GlobalMappingCollection))]
public sealed class GlobalMappingStartupTests
{
    /// <summary>
    /// Discovers a mapping file from the loaded test assembly, not only Core.
    /// </summary>
    [Fact]
    public void Registration_LoadedProfileIsAvailableToNativeAdapt()
    {
        var services = new ServiceCollection();

        services.AddMapping();
        var result = new GlobalMappingSource { Name = "sample" }
            .Adapt<GlobalMappingTarget>();

        Assert.Equal("sample", result.DisplayName);
    }
}

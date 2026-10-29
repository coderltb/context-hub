namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.DependencyInjection;

/// <summary>
/// Verifies startup compilation, process-wide mapping and local customization without DI-owned mapping state.
/// </summary>
[Collection(nameof(GlobalMappingCollection))]
public sealed class MappingRegistrationTests
{
    /// <summary>
    /// Maps an undeclared pair by convention without requiring a profile for every DTO.
    /// </summary>
    [Fact]
    public void Registration_KeepsConventionMapping()
    {
        new ServiceCollection().AddMapping();

        var result = new GlobalMappingSource { Name = "sample" }.Adapt<ConventionTarget>();

        Assert.Equal("sample", result.Name);
    }

    /// <summary>
    /// Makes mappings available across containers in one process without adding config, mapper or profile services.
    /// </summary>
    [Fact]
    public void Registration_SharesGlobalMappingsAcrossContainers()
    {
        var first = new ServiceCollection().AddMapping();
        var second = new ServiceCollection();
        using var firstHost = first.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var secondHost = second.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        var result = new GlobalMappingSource { Name = "shared" }.Adapt<GlobalMappingTarget>();

        Assert.Equal("shared", result.DisplayName);
        Assert.Empty(first);
        Assert.Empty(second);
    }

    /// <summary>
    /// Fails a bad mapping at registration rather than deferring validation until a consumer resolves from DI.
    /// </summary>
    [Fact]
    public void Registration_RejectsInvalidMappingImmediately()
    {
        var services = new ServiceCollection();

        // Only this unique pair is changed; native pair cleanup prevents a negative case poisoning later startups.
        TypeAdapterConfig.GlobalSettings.NewConfig<InvalidSource, InvalidTarget>()
            .RequireDestinationMemberSource(true);
        try
        {
            Assert.Throws<CompileException>(() => services.AddMapping());
            Assert.Empty(services);
        }
        finally
        {
            TypeAdapterConfig<InvalidSource, InvalidTarget>.Clear();
        }

        services.AddMapping();
        Assert.Equal("recovered", new GlobalMappingSource { Name = "recovered" }
            .Adapt<GlobalMappingTarget>().DisplayName);
    }

    /// <summary>
    /// Leaves already compiled mappings untouched when transport and persistence services are registered repeatedly.
    /// </summary>
    [Fact]
    public void Services_DoNotRescanOrCompileMapping()
    {
        var services = new ServiceCollection().AddMapping();
        var compiled = TypeAdapterConfig.GlobalSettings.GetMapFunction<GlobalMappingSource, GlobalMappingTarget>();

        services.AddRestClients().AddRestClients();
        services.AddPersistence<StandaloneContext>().AddPersistence<StandaloneContext>();

        Assert.Same(compiled, TypeAdapterConfig.GlobalSettings.GetMapFunction<GlobalMappingSource, GlobalMappingTarget>());
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(TypeAdapterConfig) ||
            descriptor.ServiceType == typeof(IMapper) ||
            descriptor.ServiceType == typeof(IRegister));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IRestClientMultipleService));
    }

    /// <summary>
    /// Allows explicit local customization without changing subsequent global Adapt calls.
    /// </summary>
    [Fact]
    public void LocalConfiguration_DoesNotMutateGlobalMapping()
    {
        new ServiceCollection().AddMapping();
        var input = new GlobalMappingSource { Name = "sample" };
        var local = new TypeAdapterConfig();
        local.NewConfig<GlobalMappingSource, GlobalMappingTarget>()
            .Map(destination => destination.DisplayName, source => "local:" + source.Name);

        var custom = input.Adapt<GlobalMappingTarget>(local);
        var ordinary = input.Adapt<GlobalMappingTarget>();

        Assert.Equal("local:sample", custom.DisplayName);
        Assert.Equal("sample", ordinary.DisplayName);
    }

    /// <summary>
    /// Supplies a convention-only target that never receives an explicit mapping rule.
    /// </summary>
    public sealed class ConventionTarget
    {
        /// <summary>
        /// Gets or sets the same-name member copied by native conventions.
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Isolates the invalid mapping from all production and stable fixture pairs.
    /// </summary>
    public sealed class InvalidSource
    {
        // No source member may satisfy the destination in this negative mapping scenario.
    }

    /// <summary>
    /// Requires a member that the negative case cannot obtain from its source.
    /// </summary>
    public sealed class InvalidTarget
    {
        /// <summary>
        /// Gets or sets the deliberately unmapped member.
        /// </summary>
        public string Unmapped { get; set; } = string.Empty;
    }

    /// <summary>
    /// Provides a persistence registration type without opening or configuring a database.
    /// </summary>
    private sealed class StandaloneContext : DbContext
    {
        // Composition is tested without opening or configuring a database.
    }
}

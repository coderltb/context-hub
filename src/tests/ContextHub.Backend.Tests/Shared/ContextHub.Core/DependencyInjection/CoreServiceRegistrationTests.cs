namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.DependencyInjection;

/// <summary>
/// Verifies explicit registration of Core validation and service-owned persistence dependencies.
/// </summary>
[Collection(nameof(Mappings.GlobalMappingCollection))]
public sealed class CoreServiceRegistrationTests
{
    /// <summary>
    /// Registers persistence without injecting or reconfiguring the process-wide mapper.
    /// </summary>
    [Fact]
    public void PersistenceRegistration_DoesNotRegisterMappingServices()
    {
        var services = new ServiceCollection();
        services.AddPersistence<RegistrationDbContext>().AddPersistence<RegistrationDbContext>();

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(TypeAdapterConfig) ||
            descriptor.ServiceType == typeof(IMapper) ||
            descriptor.ServiceType == typeof(IRegister));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IGenericRepository<>));
    }

    /// <summary>
    /// Allows repeated registration for one context and rejects a different owner before changing DI state.
    /// </summary>
    [Fact]
    public void PersistenceRegistration_IsIdempotentAndRejectsDifferentContext()
    {
        var services = new ServiceCollection();
        services.AddPersistence<RegistrationDbContext>();
        var descriptors = services.ToArray();

        services.AddPersistence<RegistrationDbContext>();

        Assert.Equal(descriptors, services.ToArray());
        Assert.Single(services, item => item.ServiceType == typeof(DbContext));
        Assert.Throws<InvalidOperationException>(() => services.AddPersistence<OtherRegistrationDbContext>());
        Assert.Equal(descriptors, services.ToArray());
    }

    /// <summary>
    /// Keeps the public startup surface parameterless instead of accepting feature callbacks or assembly lists.
    /// </summary>
    [Fact]
    public void MappingRegistration_ExposesOnlyParameterlessStartupEntryPoints()
    {
        foreach (var name in new[] { nameof(CoreServiceRegistration.AddCoreInfrastructure),
                     nameof(CoreServiceRegistration.AddMapping), nameof(CoreServiceRegistration.AddRestClients) })
        {
            var method = Assert.Single(typeof(CoreServiceRegistration).GetMethods(),
                candidate => candidate.Name == name);

            Assert.Equal(typeof(IServiceCollection), Assert.Single(method.GetParameters()).ParameterType);
        }
    }

    /// <summary>
    /// Resolves shared transport dependencies without requiring a mapper or configuration service.
    /// </summary>
    /// <param name="useRoot">Whether to exercise the root or direct REST registration entry point.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CoreRegistration_ResolvesSharedTransportDefaults(bool useRoot)
    {
        var services = new ServiceCollection().AddLogging();
        if (useRoot)
        {
            services.AddCoreInfrastructure();
        }
        else
        {
            services.AddRestClients();
        }

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        using var scope = provider.CreateScope();

        Assert.IsType<RestClientMultipleService>(scope.ServiceProvider.GetRequiredService<IRestClientMultipleService>());
        Assert.IsType<ThirdPartyApiService>(scope.ServiceProvider.GetRequiredService<IThirdPartyApiService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IJsonSerializerService>());
    }

    /// <summary>
    /// Keeps the delegated services guard effective for both Core composition entry points.
    /// </summary>
    /// <param name="useRoot">Whether to exercise the root or direct REST registration entry point.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CoreRegistration_RejectsMissingContainer(bool useRoot)
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            if (useRoot)
            {
                CoreServiceRegistration.AddCoreInfrastructure(null!);
            }
            else
            {
                CoreServiceRegistration.AddRestClients(null!);
            }
        });

        Assert.Equal("services", exception.ParamName);
    }

    /// <summary>
    /// Verifies that validation is registered as a transient open generic Mediator behavior.
    /// </summary>
    [Fact]
    public void AddValidationBehaviorRegistersOpenPipelineBehavior()
    {
        var services = new ServiceCollection();

        services.AddValidationBehavior();

        var descriptor = Assert.Single(
            services,
            item => item.ServiceType == typeof(IPipelineBehavior<,>));
        Assert.Equal(typeof(ValidationBehavior<,>), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
    }

    /// <summary>
    /// Verifies that repositories and Unit of Work resolve the explicitly registered service-owned context.
    /// </summary>
    [Fact]
    public void AddPersistenceUsesExplicitServiceOwnedDbContext()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<RegistrationDbContext>(options =>
            options.UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused"));

        services.AddPersistence<RegistrationDbContext>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var ownedContext = scope.ServiceProvider.GetRequiredService<RegistrationDbContext>();
        var contextAlias = scope.ServiceProvider.GetRequiredService<DbContext>();

        Assert.Same(ownedContext, contextAlias);
        Assert.IsType<GenericRepository<TestEntity>>(
            scope.ServiceProvider.GetRequiredService<IGenericRepository<TestEntity>>());
        Assert.IsType<UnitOfWork<RegistrationDbContext>>(
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
    }

    /// <summary>
    /// Provides a context type for registration checks without opening a database connection.
    /// </summary>
    /// <param name="options">The fixture's configured EF context options.</param>
    private sealed class RegistrationDbContext(DbContextOptions<RegistrationDbContext> options)
        : DbContext(options);

    /// <summary>
    /// Represents a second owner that must not silently inherit the first context's repository alias.
    /// </summary>
    /// <param name="options">The second context's native options.</param>
    private sealed class OtherRegistrationDbContext(DbContextOptions<OtherRegistrationDbContext> options)
        : DbContext(options);

    /// <summary>
    /// Provides an entity type for resolving the open generic repository registration.
    /// </summary>
    private sealed class TestEntity;
}

namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.DependencyInjection;

/// <summary>
/// Checks the opt-in DI boundary without opening PostgreSQL connections or creating a substitute provider.
/// </summary>
public sealed class AuditedDbContextRegistrationTests
{
    /// <summary>
    /// Rejects missing registration inputs before modifying the service collection.
    /// </summary>
    [Fact]
    public void Registration_RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => PersistenceServiceRegistration
            .AddAuditedDbContext<RegistrationContext>(null!, (_, _) => { }));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection()
            .AddAuditedDbContext<RegistrationContext>(null!));
    }

    /// <summary>
    /// Reuses each scoped context, options, actor and interceptor without aliasing repositories or Unit of Work.
    /// </summary>
    [Fact]
    public void Registration_PreservesScopedOwnershipAndExplicitConfiguration()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IAuditActorAccessor, RegistrationActor>();
        var callbacks = 0;
        var returned = services.AddAuditedDbContext<RegistrationContext>((provider, options) =>
        {
            // Resolving the actor here verifies that the callback receives the context's scope, not a second container.
            Assert.NotNull(provider.GetRequiredService<IAuditActorAccessor>());
            callbacks++;
            options.UseNpgsql("Host=localhost;Database=registration_only;Username=postgres;Password=unused");
        });
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        var context = first.ServiceProvider.GetRequiredService<RegistrationContext>();
        Assert.Same(context, first.ServiceProvider.GetRequiredService<RegistrationContext>());
        Assert.NotSame(context, second.ServiceProvider.GetRequiredService<RegistrationContext>());
        Assert.Same(first.ServiceProvider.GetRequiredService<DbContextOptions<RegistrationContext>>(),
            first.ServiceProvider.GetRequiredService<DbContextOptions<RegistrationContext>>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<AuditSaveChangesInterceptor>(),
            second.ServiceProvider.GetRequiredService<AuditSaveChangesInterceptor>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<IAuditActorAccessor>(),
            second.ServiceProvider.GetRequiredService<IAuditActorAccessor>());
        Assert.Equal(2, callbacks);
        Assert.Same(services, returned);
        Assert.Null(first.ServiceProvider.GetService<DbContext>());
        Assert.Null(first.ServiceProvider.GetService<IUnitOfWork>());
        Assert.Null(first.ServiceProvider.GetService<IGenericRepository<RegistrationEntity>>());
    }

    /// <summary>
    /// Leaves actor configuration mandatory rather than inventing an anonymous actor service.
    /// </summary>
    [Fact]
    public void Registration_DoesNotCreateFallbackActor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuditedDbContext<RegistrationContext>((_, options) =>
            options.UseNpgsql("Host=localhost;Database=registration_only;Username=postgres;Password=unused"));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<RegistrationContext>());
    }

    /// <summary>
    /// Accepts native options for registration-only checks without opening a database.
    /// </summary>
    /// <param name="options">The scoped native context options.</param>
    private sealed class RegistrationContext(DbContextOptions<RegistrationContext> options) : DbContext(options);

    /// <summary>
    /// Supplies an explicitly registered actor contract for the scoped DI checks.
    /// </summary>
    private sealed class RegistrationActor : IAuditActorAccessor
    {
        /// <summary>
        /// Represents a deliberately absent actor, not a fabricated identifier.
        /// </summary>
        public Guid? ActorId => null;
    }

    /// <summary>
    /// Provides a repository type argument without registering a repository as a side effect.
    /// </summary>
    private sealed class RegistrationEntity
    {
        // Only type identity is needed to verify repository registration side effects.
    }
}

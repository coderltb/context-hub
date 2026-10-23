namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.DependencyInjection;

/// <summary>
/// Checks that persistence registration binds repository staging and Unit of Work saving to the same scoped context.
/// </summary>
/// <param name="postgreSql">The disposable PostgreSQL fixture used for observable saves.</param>
[Collection(PostgreSqlCollection.Name)]
public sealed class PersistenceContextRegistrationTests(PostgreSqlFixture postgreSql)
{
    /// <summary>
    /// Rejects a pre-existing base alias without replacing registrations owned by the service.
    /// </summary>
    /// <param name="factoryAlias">Whether the previous alias uses an opaque DI factory rather than an implementation type.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PriorAlias_IsRejectedWithoutChangingRegistrations(bool factoryAlias)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddDbContext<OtherContext>(options => options.UseNpgsql(postgreSql.ConnectionString));
        services.AddDbContext<CoreTestDbContext>(options => options.UseNpgsql(postgreSql.ConnectionString));
        if (factoryAlias)
        {
            services.AddScoped<DbContext>(provider => provider.GetRequiredService<OtherContext>());
        }
        else
        {
            services.AddScoped<DbContext, OtherContext>();
        }

        var registrations = services.ToArray();

        Assert.Throws<InvalidOperationException>(() => services.AddPersistence<CoreTestDbContext>());
        Assert.Equal(registrations, services);
    }

    /// <summary>
    /// Retains one scoped context when registration is repeated and saves changes staged by the repository.
    /// </summary>
    /// <returns>The repository save and independent database assertions.</returns>
    [Fact]
    public async Task SingleContext_RepositoryAndUnitOfWorkShareTheSameScope()
    {
        await PostgreSqlTestContext.ResetAsync(postgreSql.ConnectionString);
        var services = new ServiceCollection().AddLogging();
        services.AddDbContext<CoreTestDbContext>(options => options.UseNpgsql(postgreSql.ConnectionString));

        services.AddPersistence<CoreTestDbContext>().AddPersistence<CoreTestDbContext>();
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(DbContext));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IUnitOfWork));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoreTestDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<TestEntity>>();
        var work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // A real save detects a wrong alias: staging on the other context would leave this Unit of Work with zero writes.
        await repository.AddAsync(new TestEntity { Id = Guid.NewGuid(), Name = "Same scoped owner" });
        Assert.Equal(1, await work.SaveChangesAsync());
        Assert.Same(context, scope.ServiceProvider.GetRequiredService<DbContext>());
        await using var observer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.Equal("Same scoped owner", (await observer.Entities.AsNoTracking().SingleAsync()).Name);
    }

    /// <summary>
    /// Represents a conflicting service-owned context whose registration must remain untouched.
    /// </summary>
    /// <param name="options">The native configuration for the previous context.</param>
    private sealed class OtherContext(DbContextOptions<OtherContext> options) : DbContext(options);
}

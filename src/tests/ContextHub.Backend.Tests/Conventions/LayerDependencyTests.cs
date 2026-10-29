namespace ContextHub.Backend.Tests.Conventions;

/// <summary>
/// Enforces dependency boundaries from compiled IL, including dependencies hidden inside generic types.
/// </summary>
public sealed class LayerDependencyTests
{
    /// <summary>
    /// Keeps development-only usage annotations out of the deployed assembly dependency graph.
    /// </summary>
    [Fact]
    public void UsageAnnotationsDoNotBecomeRuntimeDependencies()
    {
        var assemblies = new[]
        {
            typeof(ApiError).Assembly,
            typeof(CreateClientCommand).Assembly,
            typeof(LoggingOptions).Assembly,
            typeof(LayerDependencyTests).Assembly
        };

        foreach (var assembly in assemblies)
        {
            var references = assembly.GetReferencedAssemblies().Select(reference => reference.Name);
            Assert.DoesNotContain("JetBrains.Annotations", references);
        }
    }

    /// <summary>
    /// Keeps application use cases on service ports and DTOs rather than persistence implementations.
    /// </summary>
    [Fact]
    public void ApplicationDoesNotDependOnPersistence()
    {
        var types = Types.InAssembly(typeof(CreateClientCommand).Assembly);
        Assert.NotEmpty(types.GetTypes());
        var result = types.ShouldNot().HaveDependencyOnAny(
            "ContextHub.Authentication.Infrastructure",
            "ContextHub.Authentication.Domain.Entities",
            "ContextHub.Core.Repositories",
            "ContextHub.Core.Interfaces.Persistence",
            "Microsoft.EntityFrameworkCore").GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Preserves Domain's legitimate Identity Stores dependency without allowing application or infrastructure references.
    /// </summary>
    [Fact]
    public void DomainDoesNotDependOnOuterLayers()
    {
        var types = Types.InAssembly(typeof(User).Assembly);
        Assert.NotEmpty(types.GetTypes());
        var result = types.ShouldNot().HaveDependencyOnAny(
            "ContextHub.Authentication.Application", "ContextHub.Authentication.Infrastructure", "ContextHub.Core").GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Keeps reusable transport and persistence available without an API-layer dependency.
    /// </summary>
    [Fact]
    public void CoreDoesNotDependOnApiCommon()
    {
        var types = Types.InAssembly(typeof(CoreServiceRegistration).Assembly);
        Assert.NotEmpty(types.GetTypes());
        var result = types.ShouldNot().HaveDependencyOn("ContextHub.ApiCommon").GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Proves that the checker rejects generic entity/repository dependencies instead of passing an empty selection.
    /// </summary>
    /// <param name="fixture">The intentionally invalid test-only type.</param>
    /// <param name="dependency">The dependency forbidden by the production boundary.</param>
    [Theory]
    [InlineData(nameof(EntityListFixture), "ContextHub.Authentication.Domain.Entities")]
    [InlineData(nameof(RepositoryFixture), "ContextHub.Core.Interfaces.Persistence")]
    [InlineData(nameof(ContextFixture), "Microsoft.EntityFrameworkCore")]
    public void NegativeFixturesAreRejected(string fixture, string dependency)
    {
        var types = Types.InAssembly(typeof(LayerDependencyTests).Assembly).That().HaveName(fixture);
        Assert.Single(types.GetTypes());

        Assert.False(types.ShouldNot().HaveDependencyOn(dependency).GetResult().IsSuccessful);
    }

    /// <summary>
    /// Intentionally leaks a persisted entity through a nested generic collection.
    /// </summary>
    private sealed class EntityListFixture
    {
        /// <summary>
        /// Gets the invalid test-only entity contract.
        /// </summary>
        public Task<IReadOnlyList<User>> Entities => Task.FromResult<IReadOnlyList<User>>([]);
    }

    /// <summary>
    /// Intentionally leaks the generic repository abstraction into a forbidden layer.
    /// </summary>
    private sealed class RepositoryFixture
    {
        /// <summary>
        /// Gets the invalid test-only persistence dependency.
        /// </summary>
        public IGenericRepository<User>? Repository { get; init; }
    }

    /// <summary>
    /// Intentionally leaks an EF context through an asynchronous result.
    /// </summary>
    private sealed class ContextFixture
    {
        /// <summary>
        /// Gets the invalid test-only framework dependency.
        /// </summary>
        public Task<DbContext?> Context => Task.FromResult<DbContext?>(null);
    }
}

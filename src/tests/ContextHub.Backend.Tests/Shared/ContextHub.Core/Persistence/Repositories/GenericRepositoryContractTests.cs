namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Persistence.Repositories;

/// <summary>
/// Guards the repository boundary against exposing provider-specific IQueryable APIs.
/// </summary>
public sealed class GenericRepositoryContractTests
{
    /// <summary>
    /// Uses meaningful retrieval names while retaining familiar EF-shaped operation names.
    /// </summary>
    /// <param name="methodName">The required retrieval or existence operation name.</param>
    /// <param name="overloadCount">The existing entity and projection overload count.</param>
    [Theory]
    [InlineData("GetListAsync", 3)]
    [InlineData("FirstOrDefaultAsync", 3)]
    [InlineData("SingleOrDefaultAsync", 2)]
    [InlineData("GetPageAsync", 3)]
    [InlineData("CountAsync", 1)]
    [InlineData("AnyAsync", 1)]
    public void RetrievalOperationsUseExplicitNames(string methodName, int overloadCount)
    {
        var methods = typeof(IGenericRepository<TestEntity>).GetMethods();

        Assert.Equal(overloadCount, methods.Count(method => method.Name == methodName));
        Assert.DoesNotContain(methods, method => method.Name is
            "ListAsync" or "PageAsync" or "GetFirstOrDefaultAsync" or "GetSingleOrDefaultAsync" or "GetCountAsync" or "ExistsAsync");

        Assert.DoesNotContain(typeof(GenericRepository<TestEntity>).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic),
            method => method.Name is "QueryPageAsync" or "ReadPageAsync");
    }

    /// <summary>
    /// Keeps key-based reads on the conditional pipeline instead of exposing a separate tracked lookup.
    /// </summary>
    [Fact]
    public void ReadContractDoesNotExposeASeparateKeyLookup()
    {
        // A separate key shortcut could bypass a derived repository's mandatory scope.
        Assert.DoesNotContain(typeof(IGenericRepository<TestEntity>).GetMethods(),
            method => method.Name == "GetByIdAsync");
    }

    /// <summary>
    /// Requires mapped reads alongside selector reads and an overridable projection seam.
    /// </summary>
    [Fact]
    public void MappedReadsExposeOptionalQueryInputsAndProjectionOverride()
    {
        foreach (var name in new[] { "GetListAsync", "FirstOrDefaultAsync", "GetPageAsync" })
        {
            var methods = typeof(IGenericRepository<TestEntity>).GetMethods()
                .Where(method => method.Name == name && method.IsGenericMethod).ToArray();

            Assert.Equal(2, methods.Length);
            var mapped = Assert.Single(methods, method => method.GetParameters()[0].ParameterType ==
                (name == "GetPageAsync" ? typeof(PageRequest) : typeof(QueryOrder<TestEntity>)));
            Assert.Equal(typeof(CancellationToken), mapped.GetParameters()[^1].ParameterType);
            Assert.All(mapped.GetParameters().Skip(name == "GetPageAsync" ? 1 : 0),
                parameter => Assert.True(parameter.HasDefaultValue));
        }

        var projection = typeof(GenericRepository<TestEntity>).GetMethod("CreateProjection",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(projection);
        Assert.True(projection.IsFamily && projection.IsVirtual && !projection.IsFinal);
    }

    /// <summary>
    /// Requires a single protected query seam and explicit collection selectors without exposing queries publicly.
    /// </summary>
    [Fact]
    public void ReadPipelineSupportsSpecializationAndCollectionTextSelectors()
    {
        var method = typeof(GenericRepository<TestEntity>).GetMethod("CreateQuery",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);
        Assert.True(method.IsFamily && method.IsVirtual && !method.IsFinal);
        Assert.Equal(typeof(IQueryable<TestEntity>), method.ReturnType);
        Assert.Equal(3, method.GetParameters().Length);
        Assert.All(method.GetParameters(), parameter => Assert.True(parameter.HasDefaultValue));

        var property = typeof(SearchOptions<TestEntity>).GetProperty("CollectionTextFields");

        Assert.NotNull(property);
        Assert.Equal(typeof(IReadOnlyList<Expression<Func<TestEntity, IEnumerable<string>>>>), property.PropertyType);
    }

    /// <summary>
    /// Allows specific repositories to reuse and override CRUD while sharing the service context.
    /// </summary>
    [Fact]
    public void RepositoryCanBeSpecializedWithoutReplacingItsContext()
    {
        var repository = typeof(GenericRepository<TestEntity>);

        Assert.False(repository.IsSealed);
        Assert.All(repository.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            method => Assert.True(method.IsVirtual && !method.IsFinal));
        Assert.Contains(repository.GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            field => field.FieldType == typeof(DbContext) && field.IsFamily && field.IsInitOnly);
    }

    /// <summary>
    /// Keeps the entity-only overload available alongside typed projections without exposing provider query types.
    /// </summary>
    [Fact]
    public void PublicRepositoryContractIncludesFirstOrDefaultLookup()
    {
        var method = typeof(IGenericRepository<TestEntity>).GetMethods()
            .Single(candidate => candidate.Name == "FirstOrDefaultAsync" && !candidate.IsGenericMethod);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<TestEntity>), method.ReturnType);
        Assert.Equal(typeof(System.Linq.Expressions.Expression<Func<TestEntity, bool>>), method.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(QueryOrder<TestEntity>), method.GetParameters()[1].ParameterType);
        Assert.Equal(typeof(SearchOptions<TestEntity>), method.GetParameters()[2].ParameterType);
        Assert.Equal(typeof(CancellationToken), method.GetParameters()[3].ParameterType);
        Assert.True(method.GetParameters()[0].HasDefaultValue);
        Assert.Null(method.GetParameters()[0].DefaultValue);
        Assert.Null(method.GetParameters()[1].DefaultValue);
        Assert.Null(method.GetParameters()[2].DefaultValue);
    }

    /// <summary>
    /// Allows an unfiltered existence check through the same shared contract as filtered reads.
    /// </summary>
    [Fact]
    public void PublicRepositoryContractAllowsOptionalAnyPredicate()
    {
        var method = typeof(IGenericRepository<TestEntity>).GetMethod(nameof(IGenericRepository<TestEntity>.AnyAsync));

        Assert.NotNull(method);
        Assert.True(method.GetParameters()[0].HasDefaultValue);
        Assert.Null(method.GetParameters()[0].DefaultValue);
    }

    /// <summary>
    /// Verifies that no repository parameter or return type exposes IQueryable.
    /// </summary>
    [Fact]
    public void PublicRepositoryContractDoesNotExposeQueryable()
    {
        var publicMethods = typeof(IGenericRepository<TestEntity>).GetMethods();

        Assert.DoesNotContain(
            publicMethods,
            method => IsQueryable(method.ReturnType)
                || method.GetParameters().Any(parameter => IsQueryable(parameter.ParameterType)));
    }

    /// <summary>
    /// Detects IQueryable implementations so wrapped query types cannot bypass the contract check.
    /// </summary>
    /// <param name="type">The reflected CLR type inspected by the contract assertion.</param>
    /// <returns>Whether the tested predicate or contract condition holds.</returns>
    private static bool IsQueryable(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IQueryable<>))
        {
            return true;
        }

        return type.GetInterfaces()
            .Any(candidate => candidate.IsGenericType
                && candidate.GetGenericTypeDefinition() == typeof(IQueryable<>));
    }
}

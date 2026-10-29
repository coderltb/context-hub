namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Repositories;

/// <summary>
/// Verifies global Mapster projections on real PostgreSQL without replacing the repository query pipeline.
/// </summary>
/// <param name="postgreSql">The collection-owned disposable database, never a product connection.</param>
[Collection(PostgreSqlCollection.NAME)]
public sealed class RepositoryMappingTests(PostgreSqlFixture postgreSql) : IAsyncLifetime
{
    /// <summary>
    /// Seeds optional objects, child collections and a row outside the derived repository's scope.
    /// </summary>
    /// <returns>The isolated fixture initialization.</returns>
    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<NavigationSearchContext>().UseNpgsql(postgreSql.ConnectionString);
        await using var context = new NavigationSearchContext(options.Options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        context.AddRange(
            new NavigationProduct
            {
                Id = 1, Name = "Alpha", Category = new NavigationCategory { Id = 1, Name = "Group" },
                Variants =
                [
                    new NavigationVariant { Id = 11, Name = "needle red", Color = "red" },
                    new NavigationVariant { Id = 12, Name = "blue", Color = "blue", Stock = 3 }
                ]
            },
            new NavigationProduct
            {
                Id = 2, Name = "Beta",
                Variants = [new NavigationVariant { Id = 21, Name = "needle green", Color = "green" }]
            },
            new NavigationProduct
            {
                Id = 3, Name = "Outside", Scope = 2,
                Variants = [new NavigationVariant { Id = 31, Name = "needle red", Color = "red" }]
            },
            new NavigationProduct { Id = 4, Name = "Gamma", Active = true });

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Leaves database container ownership with the collection fixture.
    /// </summary>
    /// <returns>An already completed cleanup task.</returns>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Maps renamed fields, nullable objects and children through List, First, Single and Page resolved from DI.
    /// </summary>
    /// <returns>The real database mapping and no-tracking assertions.</returns>
    [Fact]
    public async Task MappedReadsUseRegisteredProfilesForObjectsAndCollections()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<NavigationProduct>>();
        var context = scope.ServiceProvider.GetRequiredService<NavigationSearchContext>();
        var order = QueryOrder<NavigationProduct>.By(row => row.Id);

        var rows = await repository.GetListAsync<ProductDto>(order);
        var first = await repository.FirstOrDefaultAsync<ProductDto>(order);
        var page = await repository.GetPageAsync<ProductDto>(new PageRequest(1, 2), order);
        var single = await repository.SingleOrDefaultAsync<ProductDto>(predicate: row => row.Id == 1);
        var missingObject = await repository.SingleOrDefaultAsync<ProductDto>(predicate: row => row.Id == 2);

        Assert.Equal([1, 2, 3, 4], rows.Select(row => row.Code));
        Assert.Equal("Alpha", first!.DisplayName);
        Assert.Equal("Group", first.Group!.Title);
        Assert.Equal(["blue", "needle red"], first.Options.Select(row => row.Label).Order());
        Assert.Null(rows.Single(row => row.Code == 2).Group);
        Assert.Empty(rows.Single(row => row.Code == 4).Options);
        Assert.Equal(4, page.TotalCount);
        Assert.Equal([1, 2], page.Items.Select(row => row.Code));
        Assert.Equal("Alpha", single!.DisplayName);
        Assert.Equal("Group", single.Group!.Title);
        Assert.Equal(["blue", "needle red"], single.Options.Select(row => row.Label).Order());
        Assert.Null(missingObject!.Group);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Null(context.Database.CurrentTransaction);
    }

    /// <summary>
    /// Proves scalar DTO reads select only requested columns instead of loading entities before mapping.
    /// </summary>
    /// <param name="operation">The mapped facade whose emitted items SQL must retain the narrow projection.</param>
    /// <returns>The provider SQL and mapped scalar assertions.</returns>
    [Theory]
    [InlineData("List")]
    [InlineData("First")]
    [InlineData("Single")]
    [InlineData("Page")]
    public async Task ScalarProjectionSelectsOnlyRequiredColumns(string operation)
    {
        var observer = new MappingCommandObserver();
        await using var provider = CreateProvider(observer: observer);
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<NavigationProduct>>();

        if (operation == "List")
        {
            Assert.Equal(4, (await repository.GetListAsync<NameDto>()).Count);
        }
        else if (operation == "First")
        {
            Assert.NotNull(await repository.FirstOrDefaultAsync<NameDto>());
        }
        else if (operation == "Single")
        {
            Assert.NotNull(await repository.SingleOrDefaultAsync<NameDto>(predicate: row => row.Name == "Alpha"));
        }
        else
        {
            Assert.Equal(4, (await repository.GetPageAsync<NameDto>(new PageRequest())).TotalCount);
        }

        // Page issues a separate aggregate first; inspect the items statement rather than mistaking count for projection.
        Assert.Equal(operation == "Page" ? 2 : 1, observer.Sql.Count);
        var sql = observer.Sql[^1];
        Assert.Contains("\"Name\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Id\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Stock\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Scope\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Active\"", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("JOIN", sql, StringComparison.Ordinal);
        Assert.Equal(operation == "Single", sql.Contains("WHERE", StringComparison.Ordinal));
        Assert.DoesNotContain("ORDER BY", sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// Keeps every mapped read on the virtual selector and scoped query seams, preparing each page once.
    /// </summary>
    /// <returns>The scope, child search, mixed ordering, virtual dispatch and count assertions.</returns>
    [Fact]
    public async Task MappedReadsPreserveOverridesSearchScopeAndOrdering()
    {
        var observer = new MappingCommandObserver();
        await using var provider = CreateProvider(observer: observer);
        await using var scope = provider.CreateAsyncScope();
        var specific = scope.ServiceProvider.GetRequiredService<ScopedMappedRepository>();
        IGenericRepository<NavigationProduct> repository = specific;
        var search = new SearchOptions<NavigationProduct>
        {
            SearchTerm = "needle",
            CollectionTextFields = [row => row.Variants.Select(child => child.Name)],
            FieldFilters = [row => !row.Active, row => row.Stock == 0]
        };
        var order = QueryOrder<NavigationProduct>.By(row => row.Stock)
            .ThenBy(row => row.Name, descending: true).ThenBy(row => row.Id);

        var rows = await repository.GetListAsync<ProductDto>(order, search: search);
        var first = await repository.FirstOrDefaultAsync<ProductDto>(order, search: search);
        var beforePage = specific.Preparations;
        var page = await repository.GetPageAsync<ProductDto>(new PageRequest(1, 1), order, search: search);

        Assert.Equal([2, 1], rows.Select(row => row.Code));
        Assert.Equal(2, first!.Code);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, Assert.Single(page.Items).Code);
        Assert.Equal(beforePage + 1, specific.Preparations);
        Assert.Equal(3, specific.Projections);
        Assert.Equal(3, specific.SelectorCalls);

        // Counting stays unpaged and unordered even though item selection retains mixed sort directions.
        var count = Assert.Single(observer.Sql, sql => sql.Contains("count(", StringComparison.Ordinal));
        Assert.DoesNotContain("ORDER BY", count, StringComparison.Ordinal);
        Assert.DoesNotContain("LIMIT", count, StringComparison.Ordinal);
        Assert.Contains(" DESC", observer.Sql[^1], StringComparison.Ordinal);
    }

    /// <summary>
    /// Keeps hand-written selectors, boolean overload selection and mapped no-match semantics intact.
    /// </summary>
    /// <returns>The empty, boolean and filtered-child projection assertions.</returns>
    [Fact]
    public async Task SelectorReadsAndNoMatchKeepTheirExistingMeaning()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<NavigationProduct>>();

        Assert.Null(await repository.FirstOrDefaultAsync<ProductDto>(predicate: row => row.Id == 99));
        Assert.Empty(await repository.GetListAsync<ProductDto>(predicate: row => row.Id == 99));
        var empty = await repository.GetPageAsync<ProductDto>(new PageRequest(), predicate: row => row.Id == 99);
        Assert.Empty(empty.Items);
        Assert.Equal(0, empty.TotalCount);
        Assert.Equal(4, (await repository.GetListAsync<NameDto>(order: null)).Count);
        Assert.NotNull(await repository.FirstOrDefaultAsync<NameDto>(order: null));
        Assert.Single(await repository.GetListAsync(row => row.Active));
        Assert.Equal([true], await repository.GetListAsync(row => row.Active, row => row.Id == 4));
        Assert.True(await repository.FirstOrDefaultAsync(row => row.Active, row => row.Id == 4));
        Assert.False(await repository.FirstOrDefaultAsync(row => row.Active, row => row.Id == 99));

        // A custom selector can filter returned children without altering the mapped facade or parent membership.
        var children = await repository.GetPageAsync(row => row.Variants.Where(child => child.Color == "red")
            .Select(child => child.Name).ToList(), new PageRequest(), predicate: row => row.Id == 1);

        Assert.Equal(new[] { "needle red" }, Assert.Single(children.Items));
    }

    /// <summary>
    /// Propagates cancellation on each mapped facade without saving unrelated tracked writes.
    /// </summary>
    /// <param name="operation">The mapped read exercised with an already cancelled caller token.</param>
    /// <returns>The original cancellation and unchanged staging assertions.</returns>
    [Theory]
    [InlineData("List")]
    [InlineData("First")]
    [InlineData("Page")]
    public async Task MappedReadsPropagateCancellationWithoutSaving(string operation)
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<NavigationProduct>>();
        var context = scope.ServiceProvider.GetRequiredService<NavigationSearchContext>();
        var staged = await repository.AddAsync(new NavigationProduct { Id = 9, Name = "Pending" });
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Task read = operation switch
        {
            "List" => repository.GetListAsync<ProductDto>(cancellationToken: cancellation.Token),
            "First" => repository.FirstOrDefaultAsync<ProductDto>(cancellationToken: cancellation.Token),
            _ => repository.GetPageAsync<ProductDto>(new PageRequest(), cancellationToken: cancellation.Token)
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);

        Assert.Equal(EntityState.Added, context.Entry(staged).State);
        Assert.False(await repository.AnyAsync(row => row.Id == 9));
        Assert.Null(context.Database.CurrentTransaction);
    }

    /// <summary>
    /// Shares one process-wide projection between independently composed persistence containers.
    /// </summary>
    /// <returns>The equivalent DTO results without container-owned mapping state.</returns>
    [Fact]
    public async Task PersistenceUsesGlobalMappingAcrossContainers()
    {
        await using var first = CreateProvider();
        await using var second = CreateProvider();
        await using var firstScope = first.CreateAsyncScope();
        await using var secondScope = second.CreateAsyncScope();

        var firstRow = await firstScope.ServiceProvider.GetRequiredService<IGenericRepository<NavigationProduct>>()
            .FirstOrDefaultAsync<NameDto>(predicate: row => row.Id == 1);
        var secondRow = await secondScope.ServiceProvider.GetRequiredService<IGenericRepository<NavigationProduct>>()
            .FirstOrDefaultAsync<NameDto>(predicate: row => row.Id == 1);

        Assert.Equal("Alpha", firstRow!.DisplayName);
        Assert.Equal(firstRow.DisplayName, secondRow!.DisplayName);
    }

    /// <summary>
    /// Preserves projection-building failures without database I/O or an in-memory mapping fallback.
    /// </summary>
    /// <returns>The exact exception identity and zero-query assertions.</returns>
    [Fact]
    public async Task ProjectionFailureDoesNotFallBackToEntityMaterialization()
    {
        var failure = new InvalidOperationException("Fixture projection failure");
        var observer = new MappingCommandObserver();
        await using var provider = CreateProvider(observer: observer);
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ScopedMappedRepository>();
        repository.ProjectionFailure = failure;

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.GetListAsync<NameDto>());

        Assert.Same(failure, actual);
        Assert.Empty(observer.Sql);
    }

    /// <summary>
    /// Composes real repository services after configuring stable file-based mappings at startup.
    /// </summary>
    /// <param name="observer">An optional observer of commands that reach PostgreSQL.</param>
    /// <returns>A provider whose caller owns disposal and scoped database work.</returns>
    private ServiceProvider CreateProvider(MappingCommandObserver? observer = null)
    {
        var services = new ServiceCollection().AddLogging().AddMapping();
        services.AddDbContext<NavigationSearchContext>(options =>
        {
            options.UseNpgsql(postgreSql.ConnectionString);
            if (observer is not null)
            {
                options.AddInterceptors(observer);
            }
        });

        // Persistence can be composed repeatedly without scanning profiles or creating mapper services.
        services.AddPersistence<NavigationSearchContext>().AddPersistence<NavigationSearchContext>();
        services.AddScoped<ScopedMappedRepository>();

        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(TypeAdapterConfig) || descriptor.ServiceType == typeof(IMapper));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IGenericRepository<>));

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <summary>
    /// Demonstrates specialization of projection, selector dispatch and scope through DI.
    /// </summary>
    /// <param name="context">The scoped service-owned context.</param>
    /// <param name="logger">The metadata-only repository logger.</param>
    private sealed class ScopedMappedRepository(DbContext context, ILogger<GenericRepository<NavigationProduct>> logger)
        : GenericRepository<NavigationProduct>(context, logger)
    {
        /// <summary>
        /// Counts query preparations rather than executed SQL statements.
        /// </summary>
        public int Preparations { get; private set; }

        /// <summary>
        /// Injects a projection override failure without mutating global configuration or database state.
        /// </summary>
        public Exception? ProjectionFailure { get; set; }

        /// <summary>
        /// Counts requests for the overridable mapping projection.
        /// </summary>
        public int Projections { get; private set; }

        /// <summary>
        /// Counts virtual forwarding to selector overloads.
        /// </summary>
        public int SelectorCalls { get; private set; }

        /// <summary>
        /// Adds the repository's mandatory scope before delegating optional search and ordering.
        /// </summary>
        /// <param name="predicate">The optional caller scope or filter.</param>
        /// <param name="search">The optional text and field filters.</param>
        /// <param name="order">The optional multi-key ordering.</param>
        /// <returns>The scoped provider query without execution.</returns>
        protected override IQueryable<NavigationProduct> CreateQuery(Expression<Func<NavigationProduct, bool>>? predicate = null,
            SearchOptions<NavigationProduct>? search = null, QueryOrder<NavigationProduct>? order = null)
        {
            Preparations++;
            Expression<Func<NavigationProduct, bool>> scope = row => row.Scope == 1;

            return base.CreateQuery(scope.AndAlso(predicate), search, order);
        }

        /// <summary>
        /// Observes projection specialization without replacing the registered mapping behavior.
        /// </summary>
        /// <typeparam name="TResult">The requested mapped result type.</typeparam>
        /// <returns>The globally configured provider expression.</returns>
        protected override Expression<Func<NavigationProduct, TResult>> CreateProjection<TResult>()
        {
            Projections++;

            // Projection overrides must fail before I/O; the mapped facade must not switch to entity materialization.
            if (ProjectionFailure is { } failure)
            {
                throw failure;
            }

            return base.CreateProjection<TResult>();
        }

        /// <summary>
        /// Verifies that mapped GetListAsync still dispatches to an overridden selector method.
        /// </summary>
        /// <typeparam name="TResult">The selected result type.</typeparam>
        /// <param name="selector">The mapped or hand-written selector.</param>
        /// <param name="predicate">The optional caller predicate.</param>
        /// <param name="order">The optional ordering.</param>
        /// <param name="search">The optional search.</param>
        /// <param name="cancellationToken">The caller cancellation token.</param>
        /// <returns>The selected rows from the base query pipeline.</returns>
        public override Task<IReadOnlyList<TResult>> GetListAsync<TResult>(Expression<Func<NavigationProduct, TResult>> selector,
            Expression<Func<NavigationProduct, bool>>? predicate = null, QueryOrder<NavigationProduct>? order = null,
            SearchOptions<NavigationProduct>? search = null, CancellationToken cancellationToken = default)
        {
            SelectorCalls++;

            return base.GetListAsync(selector, predicate, order, search, cancellationToken);
        }

        /// <summary>
        /// Verifies that mapped First still dispatches to an overridden selector method.
        /// </summary>
        /// <typeparam name="TResult">The selected result type.</typeparam>
        /// <param name="selector">The mapped or hand-written selector.</param>
        /// <param name="predicate">The optional caller predicate.</param>
        /// <param name="order">The optional ordering.</param>
        /// <param name="search">The optional search.</param>
        /// <param name="cancellationToken">The caller cancellation token.</param>
        /// <returns>The selected row or its native default when no row matches.</returns>
        public override Task<TResult?> FirstOrDefaultAsync<TResult>(Expression<Func<NavigationProduct, TResult>> selector,
            Expression<Func<NavigationProduct, bool>>? predicate = null, QueryOrder<NavigationProduct>? order = null,
            SearchOptions<NavigationProduct>? search = null, CancellationToken cancellationToken = default)
            where TResult : default
        {
            SelectorCalls++;

            return base.FirstOrDefaultAsync(selector, predicate, order, search, cancellationToken);
        }

        /// <summary>
        /// Verifies that mapped Page still dispatches to an overridden selector method.
        /// </summary>
        /// <typeparam name="TResult">The selected result type.</typeparam>
        /// <param name="selector">The mapped or hand-written selector.</param>
        /// <param name="page">The bounded page request.</param>
        /// <param name="order">The optional ordering.</param>
        /// <param name="predicate">The optional caller predicate.</param>
        /// <param name="search">The optional search.</param>
        /// <param name="cancellationToken">The caller cancellation token.</param>
        /// <returns>The selected page and independent count.</returns>
        public override Task<PageResponse<TResult>> GetPageAsync<TResult>(Expression<Func<NavigationProduct, TResult>> selector,
            PageRequest page, QueryOrder<NavigationProduct>? order = null, Expression<Func<NavigationProduct, bool>>? predicate = null,
            SearchOptions<NavigationProduct>? search = null, CancellationToken cancellationToken = default)
        {
            SelectorCalls++;

            return base.GetPageAsync(selector, page, order, predicate, search, cancellationToken);
        }
    }

    /// <summary>
    /// Captures command text only so assertions distinguish SQL projection from entity materialization.
    /// </summary>
    private sealed class MappingCommandObserver : DbCommandInterceptor
    {
        /// <summary>
        /// Holds SQL issued by mapped reads, excluding fixture initialization.
        /// </summary>
        public List<string> Sql { get; } = [];

        /// <summary>
        /// Records provider execution without changing its result or cancellation.
        /// </summary>
        /// <param name="command">The command sent to PostgreSQL.</param>
        /// <param name="eventData">The native command context.</param>
        /// <param name="result">The existing interception result.</param>
        /// <param name="cancellationToken">The caller query token.</param>
        /// <returns>The unchanged interception result.</returns>
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Sql.Add(command.CommandText);

            return ValueTask.FromResult(result);
        }
    }

    /// <summary>
    /// Selects one renamed scalar without requesting the entity's other columns.
    /// </summary>
    public sealed class NameDto
    {
        /// <summary>
        /// Receives the mapped product name.
        /// </summary>
        public string DisplayName { get; set; } = "";
    }

    /// <summary>
    /// Selects renamed parent data and explicitly mapped related DTOs.
    /// </summary>
    public sealed class ProductDto
    {
        /// <summary>
        /// Receives the parent's identifier under a different CLR member name.
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// Receives the profile-configured display name.
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// Keeps an absent related object null.
        /// </summary>
        public CategoryDto? Group { get; set; }

        /// <summary>
        /// Receives the mapped child collection without requiring Include.
        /// </summary>
        public List<VariantDto> Options { get; set; } = [];
    }

    /// <summary>
    /// Selects the optional category name under its DTO name.
    /// </summary>
    public sealed class CategoryDto
    {
        /// <summary>
        /// Receives the nullable related-object name.
        /// </summary>
        public string? Title { get; set; }
    }

    /// <summary>
    /// Selects only the child data explicitly requested by its mapping.
    /// </summary>
    public sealed class VariantDto
    {
        /// <summary>
        /// Receives the nullable child name.
        /// </summary>
        public string? Label { get; set; }
    }
}

namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Persistence.Repositories;

/// <summary>
/// Verifies navigation search, child projections and the shared query seam against PostgreSQL.
/// </summary>
/// <param name="postgreSql">The collection-owned disposable database.</param>
[Collection(PostgreSqlCollection.Name)]
public sealed class RepositoryNavigationSearchTests(PostgreSqlFixture postgreSql) : IAsyncLifetime
{
    /// <summary>
    /// Seeds root-only, object-only, child-only, out-of-scope and null navigation cases.
    /// </summary>
    /// <returns>The fixture-only database preparation.</returns>
    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();

        context.AddRange(
            new NavigationProduct { Id = 1, Name = "needle root" },
            new NavigationProduct
            {
                Id = 2, Name = "plain", Category = new NavigationCategory { Id = 2, Name = "needle category" },
                Variants =
                [
                    new NavigationVariant
                    {
                        Id = 21, Name = "needle variant", Color = "red",
                        Labels = [new NavigationLabel { Id = 211, Text = "deep needle" }]
                    },
                    new NavigationVariant { Id = 22, Name = "other", Color = "blue", Stock = 5 }
                ]
            },
            new NavigationProduct
            {
                Id = 3, Name = "plain",
                Variants =
                [
                    new NavigationVariant { Id = 31, Name = "needle variant", Color = "red", Stock = 5 },
                    new NavigationVariant { Id = 32, Name = "needle second", Color = "blue" }
                ]
            },
            new NavigationProduct
            {
                Id = 4, Name = "ordinary", Active = true, Stock = 1,
                Category = new NavigationCategory { Id = 4, Name = "ordinary" },
                Variants = [new NavigationVariant { Id = 41, Name = "other", Color = "red" }]
            },
            new NavigationProduct
            {
                Id = 5, Name = "needle outside", Scope = 2,
                Category = new NavigationCategory { Id = 5, Name = "needle category" },
                Variants = [new NavigationVariant { Id = 51, Name = "needle variant", Color = "red", Stock = 5 }]
            },
            new NavigationProduct { Id = 6, Name = "plain" },
            new NavigationProduct
            {
                Id = 7, Name = "plain", Category = new NavigationCategory { Id = 7, Name = "ordinary" },
                Variants =
                [
                    new NavigationVariant
                    {
                        Id = 71, Labels = [new NavigationLabel { Id = 711, Text = "deep needle" }]
                    }
                ]
            });
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Leaves container cleanup with the shared collection fixture.
    /// </summary>
    /// <returns>An already completed task.</returns>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// ORs root, optional object and child text while preserving scope, false and zero filters in every read shape.
    /// </summary>
    /// <returns>The real provider search, aggregate, projection and ownership assertions.</returns>
    [Fact]
    public async Task NavigationTextSharesScopeAndFiltersAcrossAllReadShapes()
    {
        var observer = new NavigationCommandObserver();
        await using var context = CreateContext(observer);
        var repository = CreateRepository(context);
        var search = new SearchOptions<NavigationProduct>
        {
            SearchTerm = "needle",
            TextFields = [row => row.Name, row => row.Category == null ? null : row.Category.Name],
            CollectionTextFields = [row => row.Variants.Select(child => child.Name)],
            FieldFilters = [null, row => !row.Active, row => row.Stock == 0]
        };
        Expression<Func<NavigationProduct, bool>> scope = row => row.Scope == 1;
        var order = QueryOrder<NavigationProduct>.By(row => row.Name).ThenBy(row => row.Id, descending: true);

        var page = await repository.GetPageAsync(row => row.Id, new PageRequest(1, 2), order, scope, search);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(new[] { 1, 3 }, page.Items);
        Assert.Equal(new[] { 1, 3, 2 }, (await repository.GetListAsync(scope, order, search)).Select(row => row.Id));
        Assert.Equal(new[] { 1, 3, 2 }, await repository.GetListAsync(row => row.Id, scope, order, search));
        Assert.Equal(1, (await repository.FirstOrDefaultAsync(scope, order, search))!.Id);
        Assert.Equal(1, await repository.FirstOrDefaultAsync(row => row.Id, scope, order, search));
        Assert.Equal(3, await repository.CountAsync(scope, search));
        Assert.True(await repository.AnyAsync(scope, search));
        Assert.False(await repository.AnyAsync(row => row.Scope == 3, search));
        Assert.Equal(new[] { 2 }, (await repository.GetPageAsync(new PageRequest(2, 2), order, scope, search)).Items.Select(row => row.Id));

        // Child predicates do not multiply parent rows; entity tracking does not create a transaction boundary.
        Assert.Contains("EXISTS", observer.Sql[0], StringComparison.Ordinal);
        Assert.DoesNotContain("ORDER BY", observer.Sql[0], StringComparison.Ordinal);
        Assert.DoesNotContain("LIMIT", observer.Sql[0], StringComparison.Ordinal);
        Assert.DoesNotContain("OFFSET", observer.Sql[0], StringComparison.Ordinal);
        Assert.Contains(" DESC", observer.Sql[1], StringComparison.Ordinal);
        Assert.Contains(observer.Parameters[0], value => value.Contains("needle", StringComparison.Ordinal));
        Assert.Equal(3, context.ChangeTracker.Entries<NavigationProduct>().Count());
        Assert.Null(context.Database.CurrentTransaction);
    }

    /// <summary>
    /// Supports collection-only search and native SelectMany across multiple child levels.
    /// </summary>
    /// <returns>The empty collection, null string and multi-level translation assertions.</returns>
    [Fact]
    public async Task CollectionOnlyAndGrandchildSearchTranslateWithoutScalarSelectors()
    {
        await using var context = CreateContext();
        var repository = CreateRepository(context);
        var direct = new SearchOptions<NavigationProduct>
        {
            SearchTerm = "needle", CollectionTextFields = [row => row.Variants.Select(child => child.Name)]
        };
        var nested = new SearchOptions<NavigationProduct>
        {
            SearchTerm = "needle",
            CollectionTextFields = [row => row.Variants.SelectMany(child => child.Labels.Select(label => label.Text))]
        };

        Assert.Equal(new[] { 2, 3 }, await repository.GetListAsync(row => row.Id, row => row.Scope == 1,
            QueryOrder<NavigationProduct>.By(row => row.Id), direct));
        Assert.Equal(new[] { 2, 7 }, await repository.GetListAsync(row => row.Id,
            order: QueryOrder<NavigationProduct>.By(row => row.Id), search: nested));

        // Separate child sources are alternatives, not a requirement that both collection levels match.
        var alternatives = new SearchOptions<NavigationProduct>
        {
            SearchTerm = "needle",
            CollectionTextFields = [direct.CollectionTextFields![0], nested.CollectionTextFields![0]]
        };

        Assert.Equal(new[] { 2, 3, 7 }, await repository.GetListAsync(row => row.Id, row => row.Scope == 1,
            QueryOrder<NavigationProduct>.By(row => row.Id), alternatives));
    }

    /// <summary>
    /// Matches an optional object alone and leaves both search and ordering absent unless requested.
    /// </summary>
    /// <returns>The nullable navigation and no-implicit-order SQL assertions.</returns>
    [Fact]
    public async Task ObjectOnlySearchAndAbsentSearchOrderKeepTheirOwnSemantics()
    {
        var observer = new NavigationCommandObserver();
        await using var context = CreateContext(observer);
        var repository = CreateRepository(context);
        var search = new SearchOptions<NavigationProduct>
        {
            SearchTerm = "needle", TextFields = [row => row.Category == null ? null : row.Category.Name]
        };

        Assert.Equal(new[] { 2 }, await repository.GetListAsync(row => row.Id, row => row.Scope == 1, search: search));
        Assert.Equal(7, (await repository.GetListAsync(search: null, order: null)).Count);
        Assert.DoesNotContain("ORDER BY", observer.Sql[^1], StringComparison.Ordinal);
        Assert.DoesNotContain("WHERE", observer.Sql[^1], StringComparison.Ordinal);
        Assert.Equal(7, (await repository.GetPageAsync(new PageRequest(), order: null, search: null)).TotalCount);
        Assert.DoesNotContain("ORDER BY", observer.Sql[^1], StringComparison.Ordinal);
    }

    /// <summary>
    /// Preserves caller-defined same-child grouping rather than merging separate Any predicates.
    /// </summary>
    /// <returns>The same-child, independent-child, set, range and null comparison assertions.</returns>
    [Fact]
    public async Task FieldFiltersPreserveChildGroupingAndExplicitValues()
    {
        await using var context = CreateContext();
        var repository = CreateRepository(context);
        var colors = new[] { "red", "blue" };
        var sameChild = new SearchOptions<NavigationProduct>
        {
            FieldFilters = [row => row.Variants.Any(child => child.Color == "red" && child.Stock > 0)]
        };
        var separateChildren = new SearchOptions<NavigationProduct>
        {
            SearchTerm = " \t ",
            FieldFilters = [row => row.Variants.Any(child => child.Color == "red"),
                row => row.Variants.Any(child => child.Stock > 0)]
        };
        var rangeAndSet = new SearchOptions<NavigationProduct>
        {
            FieldFilters = [row => row.Variants.Any(child => colors.Contains(child.Color!) && child.Stock >= 5 && child.Stock <= 5)]
        };
        var nullField = new SearchOptions<NavigationProduct>
        {
            FieldFilters = [row => row.Variants.Any(child => child.Color == null && child.Stock == 0)]
        };

        Assert.Equal(new[] { 3 }, await repository.GetListAsync(row => row.Id, row => row.Scope == 1, search: sameChild));
        Assert.Equal(new[] { 2, 3 }, (await repository.GetListAsync(row => row.Id, row => row.Scope == 1,
            search: separateChildren)).Order());
        Assert.Equal(2, await repository.CountAsync(row => row.Scope == 1, rangeAndSet));
        Assert.Equal(new[] { 7 }, await repository.GetListAsync(row => row.Id, search: nullField));
    }

    /// <summary>
    /// Lets projection independently choose all children, matching children or no children without changing parents.
    /// </summary>
    /// <returns>The database-side child filtering, sorting, empty and nullable-object assertions.</returns>
    [Fact]
    public async Task ChildProjectionDoesNotChangeTheParentSetOrTotal()
    {
        await using var context = CreateContext();
        var repository = CreateRepository(context);
        var search = new SearchOptions<NavigationProduct>
        {
            FieldFilters = [row => row.Variants.Any(child => child.Color == "red")]
        };
        var order = QueryOrder<NavigationProduct>.By(row => row.Id);

        var all = await repository.GetPageAsync(row => new
        {
            row.Id, Category = row.Category == null ? null : row.Category.Name,
            Children = row.Variants.OrderByDescending(child => child.Id).Select(child => child.Id).ToList()
        }, new PageRequest(), order, row => row.Scope == 1, search);
        var filtered = await repository.GetPageAsync(row => new
        {
            row.Id, Children = row.Variants.Where(child => child.Color == "red")
                .OrderBy(child => child.Id).Select(child => child.Id).ToList()
        }, new PageRequest(), order, row => row.Scope == 1, search);
        var empty = await repository.GetListAsync(row => new
        {
            row.Id, Children = row.Variants.Where(child => child.Color == "missing").Select(child => child.Id).ToList()
        }, row => row.Scope == 1, order, search);

        Assert.Equal(3, all.TotalCount);
        Assert.Equal(all.TotalCount, filtered.TotalCount);
        Assert.Equal(new[] { 2, 3, 4 }, all.Items.Select(row => row.Id));
        Assert.Equal(all.Items.Select(row => row.Id), filtered.Items.Select(row => row.Id));
        Assert.Equal(new[] { 22, 21 }, all.Items[0].Children);
        Assert.Equal(new[] { 21 }, filtered.Items[0].Children);
        Assert.Null(all.Items[1].Category);
        Assert.All(empty, row => Assert.Empty(row.Children));
        Assert.Empty(context.ChangeTracker.Entries());

        // Search does not load or trim navigation; an entity-only read leaves related data unloaded.
        var parent = await repository.FirstOrDefaultAsync(predicate: row => row.Id == 2);

        Assert.Null(parent!.Category);
        Assert.Empty(parent.Variants);
        var unfilteredParents = await repository.GetListAsync(row => new
        {
            row.Id, Children = row.Variants.Select(child => child.Id).ToList()
        }, row => row.Scope == 1);

        Assert.Equal(6, unfilteredParents.Count);
        Assert.Empty(unfilteredParents.Single(row => row.Id == 6).Children);
    }

    /// <summary>
    /// Keeps key predicates inside the same scoped query pipeline with native entity tracking.
    /// </summary>
    /// <returns>The scope override and native identity assertions.</returns>
    [Fact]
    public async Task KeyPredicateReadRespectsQueryOverrideScope()
    {
        await using var context = CreateContext();
        var specific = new ScopedNavigationRepository(context);
        IGenericRepository<NavigationProduct> repository = specific;

        // No key shortcut may bypass the scope supplied by the derived repository.
        var outsideScope = await repository.FirstOrDefaultAsync(predicate: row => row.Id == 5);
        var insideScope = await repository.FirstOrDefaultAsync(predicate: row => row.Id == 1);

        Assert.Null(outsideScope);
        Assert.NotNull(insideScope);
        Assert.Equal(1, insideScope.Id);
        Assert.Equal(2, specific.Preparations);
        Assert.Same(insideScope, Assert.Single(context.ChangeTracker.Entries<NavigationProduct>()).Entity);
    }

    /// <summary>
    /// Makes one query override authoritative for conditional reads and both paging shapes.
    /// </summary>
    /// <returns>The interface dispatch, single preparation, cancellation and no-save assertions.</returns>
    [Fact]
    public async Task OneOverrideScopesConditionalReadsAndPreparesEachPageOnce()
    {
        await using var context = CreateContext();
        var specific = new ScopedNavigationRepository(context);
        IGenericRepository<NavigationProduct> repository = specific;

        Assert.Null(await repository.FirstOrDefaultAsync(predicate: row => row.Id == 5));
        Assert.Equal(1, specific.Preparations);
        Assert.Equal(1, (await repository.FirstOrDefaultAsync(predicate: row => row.Id == 1))!.Id);
        Assert.Equal(6, (await repository.GetListAsync()).Count);
        Assert.Equal(6, (await repository.GetListAsync(row => row.Id)).Count);
        Assert.NotNull(await repository.FirstOrDefaultAsync());
        Assert.NotEqual(5, await repository.FirstOrDefaultAsync(row => row.Id));
        Assert.True(await repository.AnyAsync());
        Assert.Equal(6, await repository.CountAsync());
        Assert.Equal(8, specific.Preparations);

        var beforePage = specific.Preparations;
        Assert.Equal(6, (await repository.GetPageAsync(new PageRequest())).TotalCount);
        Assert.Equal(beforePage + 1, specific.Preparations);
        Assert.Equal(6, (await repository.GetPageAsync(row => row.Id, new PageRequest())).TotalCount);
        Assert.Equal(beforePage + 2, specific.Preparations);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetPageAsync(new PageRequest(),
            cancellationToken: cancellation.Token));
        Assert.Equal(6, context.ChangeTracker.Entries<NavigationProduct>().Count());
        Assert.Null(context.Database.CurrentTransaction);

        // A read must not persist unrelated staged writes or introduce a transaction boundary.
        var staged = await repository.AddAsync(new NavigationProduct { Id = 9, Name = "pending" });

        Assert.Equal(6, await repository.CountAsync());
        Assert.Equal(EntityState.Added, context.Entry(staged).State);
        Assert.Null(context.Database.CurrentTransaction);
    }

    /// <summary>
    /// Snapshots collection selectors before count and keeps native object/aggregate ordering provider-side.
    /// </summary>
    /// <returns>The shared-query preparation and SQL aggregate-ordering assertions.</returns>
    [Fact]
    public async Task PagingSnapshotsCollectionSelectorsAndSupportsNavigationOrdering()
    {
        var fields = new List<Expression<Func<NavigationProduct, IEnumerable<string?>>>>
            { row => row.Variants.Select(child => child.Name) };
        var observer = new NavigationCommandObserver(() => fields.Clear());
        await using var context = CreateContext(observer);
        var repository = new ScopedNavigationRepository(context);
        var search = new SearchOptions<NavigationProduct> { SearchTerm = "needle", CollectionTextFields = fields };
        var order = QueryOrder<NavigationProduct>.By(row => row.Category == null ? null : row.Category.Name)
            .ThenBy(row => row.Variants.Count, descending: true).ThenBy(row => row.Id);

        var page = await repository.GetPageAsync(row => row.Id, new PageRequest(), order, search: search);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(new[] { 2, 3 }, page.Items);
        Assert.Equal(1, repository.Preparations);
        Assert.Empty(fields);
        Assert.Equal(2, observer.Sql.Count);
        Assert.DoesNotContain("ORDER BY", observer.Sql[0], StringComparison.Ordinal);
        Assert.Contains("ORDER BY", observer.Sql[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// Keeps wildcard-looking text literal and parameterized even inside a child Any predicate.
    /// </summary>
    /// <param name="term">The unchanged literal keyword.</param>
    /// <returns>The PostgreSQL exact substring and parameter assertions.</returns>
    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("'")]
    [InlineData("\\")]
    [InlineData("Đắk")]
    public async Task CollectionKeywordIsLiteralAndParameterized(string term)
    {
        await using (var writer = CreateContext())
        {
            writer.Add(new NavigationProduct
            {
                Id = 8, Name = "plain", Variants = [new NavigationVariant { Id = 81, Name = "before" + term + "after" }]
            });
            await writer.SaveChangesAsync();
        }

        var observer = new NavigationCommandObserver();
        await using var context = CreateContext(observer);
        var search = new SearchOptions<NavigationProduct>
        {
            SearchTerm = term, CollectionTextFields = [row => row.Variants.Select(child => child.Name)]
        };

        Assert.Equal(new[] { 8 }, await CreateRepository(context).GetListAsync(row => row.Id, search: search));
        Assert.Contains(Assert.Single(observer.Parameters), value => value.Contains(term, StringComparison.Ordinal));
        Assert.Contains("@", Assert.Single(observer.Sql), StringComparison.Ordinal);
    }

    /// <summary>
    /// Rejects invalid active collection selectors before SQL but ignores selectors when text is inactive.
    /// </summary>
    /// <returns>The optional-input and pre-I/O validation assertions.</returns>
    [Fact]
    public async Task InvalidActiveCollectionSelectorFailsBeforeIo()
    {
        var observer = new NavigationCommandObserver();
        await using var context = CreateContext(observer);
        var repository = CreateRepository(context);
        var invalid = new SearchOptions<NavigationProduct>
        {
            SearchTerm = "needle", TextFields = [row => row.Name], CollectionTextFields = [null!]
        };

        await Assert.ThrowsAsync<ArgumentException>(() => repository.CountAsync(search: invalid));
        Assert.Empty(observer.Sql);
        Assert.Equal(6, await repository.CountAsync(search: new SearchOptions<NavigationProduct>
        {
            SearchTerm = " ", CollectionTextFields = [null!], FieldFilters = [row => row.Scope == 1]
        }));
    }

    /// <summary>
    /// Creates a PostgreSQL context with optional test-owned observation.
    /// </summary>
    /// <param name="observer">The optional SQL observer.</param>
    /// <returns>A context connected only to the disposable fixture database.</returns>
    private NavigationSearchContext CreateContext(DbCommandInterceptor? observer = null)
    {
        var options = new DbContextOptionsBuilder<NavigationSearchContext>().UseNpgsql(postgreSql.ConnectionString);
        if (observer is not null)
        {
            options.AddInterceptors(observer);
        }

        return new NavigationSearchContext(options.Options);
    }

    /// <summary>
    /// Creates the shared implementation without introducing test-specific query behavior.
    /// </summary>
    /// <param name="context">The fixture-owned context.</param>
    /// <returns>The generic repository sharing that context.</returns>
    private static GenericRepository<NavigationProduct> CreateRepository(DbContext context)
        => new(context, NullLogger<GenericRepository<NavigationProduct>>.Instance);

    /// <summary>
    /// Demonstrates one override adding a mandatory scope without replacing any public read method.
    /// </summary>
    /// <param name="context">The shared fixture context.</param>
    private sealed class ScopedNavigationRepository(DbContext context)
        : GenericRepository<NavigationProduct>(context, NullLogger<GenericRepository<NavigationProduct>>.Instance)
    {
        /// <summary>
        /// Counts actual pipeline preparation rather than database statements.
        /// </summary>
        public int Preparations { get; private set; }

        /// <summary>
        /// Adds scope with AND and delegates search, snapshotting and ordering to the common pipeline.
        /// </summary>
        /// <param name="predicate">The optional caller predicate.</param>
        /// <param name="search">The optional caller search.</param>
        /// <param name="order">The optional caller ordering.</param>
        /// <returns>The scoped, unexecuted persistence query.</returns>
        protected override IQueryable<NavigationProduct> CreateQuery(Expression<Func<NavigationProduct, bool>>? predicate = null,
            SearchOptions<NavigationProduct>? search = null, QueryOrder<NavigationProduct>? order = null)
        {
            Preparations++;
            Expression<Func<NavigationProduct, bool>> scope = row => row.Scope == 1;

            return base.CreateQuery(scope.AndAlso(predicate), search, order);
        }
    }

    /// <summary>
    /// Observes executed SQL and coordinates selector mutation after query preparation without sleeps.
    /// </summary>
    /// <param name="beforeItems">An optional mutation before the paging items command.</param>
    private sealed class NavigationCommandObserver(Action? beforeItems = null) : DbCommandInterceptor
    {
        /// <summary>
        /// Stores executed SQL for server-side filtering and aggregate checks.
        /// </summary>
        public List<string> Sql { get; } = [];

        /// <summary>
        /// Stores test-only parameter values for literal keyword assertions.
        /// </summary>
        public List<string[]> Parameters { get; } = [];

        /// <summary>
        /// Records real provider commands without suppressing execution.
        /// </summary>
        /// <param name="command">The provider command.</param>
        /// <param name="eventData">The native query metadata.</param>
        /// <param name="result">The unchanged interception result.</param>
        /// <param name="cancellationToken">The original query token.</param>
        /// <returns>The unchanged result after optional deterministic mutation.</returns>
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Sql.Add(command.CommandText);
            Parameters.Add(command.Parameters.Cast<DbParameter>().Select(parameter => parameter.Value?.ToString() ?? "").ToArray());
            if (command.CommandText.Contains("LIMIT", StringComparison.Ordinal))
            {
                beforeItems?.Invoke();
            }

            return ValueTask.FromResult(result);
        }
    }
}

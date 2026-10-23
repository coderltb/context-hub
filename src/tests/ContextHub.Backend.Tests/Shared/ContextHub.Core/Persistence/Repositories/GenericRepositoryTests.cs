namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Persistence.Repositories;

/// <summary>
/// Verifies repository staging, scoped native-tracking entity queries and safe logs against PostgreSQL.
/// </summary>
/// <param name="postgreSql">The shared PostgreSQL fixture supplying the real integration-test database.</param>
[Collection(PostgreSqlCollection.Name)]
public sealed class GenericRepositoryTests(PostgreSqlFixture postgreSql) : IAsyncLifetime
{
    /// <summary>
    /// Resets the shared test database before this test instance executes.
    /// </summary>
    /// <returns>A task that completes after the fixture has initialized its test dependencies.</returns>
    public Task InitializeAsync() => PostgreSqlTestContext.ResetAsync(postgreSql.ConnectionString);

    /// <summary>
    /// Completes per-test cleanup; the collection fixture owns container disposal.
    /// </summary>
    /// <returns>A task that completes after fixture-owned resources have been released.</returns>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Counts and projects in PostgreSQL without loading unused columns or tracking returned entities.
    /// </summary>
    /// <returns>The count, SQL translation and no-tracking assertions.</returns>
    [Fact]
    public async Task CountAndSelect_UseOptionalFiltersAndServerProjection()
    {
        await using (var seed = PostgreSqlTestContext.Create(postgreSql.ConnectionString))
        {
            seed.AddRange(new TestEntity { Id = Guid.NewGuid(), Name = "Match" },
                new TestEntity { Id = Guid.NewGuid(), Name = "Other" });
            await seed.SaveChangesAsync();
        }

        var commands = new List<string>();
        var options = new DbContextOptionsBuilder<CoreTestDbContext>().UseNpgsql(postgreSql.ConnectionString)
            .LogTo(commands.Add, [Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuted]).Options;
        await using var context = new CoreTestDbContext(options);
        IGenericRepository<TestEntity> repository = new GenericRepository<TestEntity>(context, NullLogger<GenericRepository<TestEntity>>.Instance);

        Assert.Equal(2L, await repository.CountAsync());
        Assert.Equal(2L, await repository.CountAsync(null));
        Assert.Equal(1L, await repository.CountAsync(entity => entity.Name == "Match"));
        var names = await repository.GetListAsync(entity => entity.Name, entity => entity.Name == "Match");

        Assert.Equal("Match", Assert.Single(names));
        Assert.Contains(commands, command => command.Contains("SELECT c.\"Name\"", StringComparison.Ordinal) &&
            !command.Contains("c.\"Id\"", StringComparison.Ordinal));
        Assert.Equal(2, (await repository.GetListAsync(entity => new NameProjection(entity.Name))).Count);
        Assert.Equal(2, (await repository.GetListAsync(entity => entity.Name, null)).Count);
        Assert.Equal(2, (await repository.GetListAsync<bool>(entity => entity.Name == "Match")).Count);
        Assert.True(await repository.FirstOrDefaultAsync<bool>(entity => entity.Name == "Match", entity => entity.Name == "Match"));
        Assert.Equal("Match", await repository.FirstOrDefaultAsync(entity => entity.Name, entity => entity.Name == "Match"));
        Assert.NotNull(await repository.FirstOrDefaultAsync(entity => new NameProjection(entity.Name), null));
        Assert.Null(await repository.FirstOrDefaultAsync(entity => entity.Name, entity => false));
        Assert.Equal(0, await repository.FirstOrDefaultAsync(entity => entity.Name.Length, entity => false));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    /// <summary>
    /// Requires a selector and preserves cancellation without treating a null predicate as an error.
    /// </summary>
    /// <returns>The projection argument and cancellation assertions.</returns>
    [Fact]
    public async Task ProjectionReads_RejectMissingSelectorAndHonorCancellation()
    {
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var repository = new GenericRepository<TestEntity>(context, NullLogger<GenericRepository<TestEntity>>.Instance);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Name the required selector: a positional null now also describes a valid omitted mapping order.
        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.GetListAsync<string>(selector: null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.FirstOrDefaultAsync<string>(selector: null!));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.CountAsync(null, cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetListAsync(entity => entity.Name, null, cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.FirstOrDefaultAsync(entity => entity.Name, null, cancellationToken: cancellation.Token));
    }

    /// <summary>
    /// Stages range changes and explicitly discards all tracking without saving or ending an outer transaction.
    /// </summary>
    /// <returns>The independent database and tracking assertions.</returns>
    [Fact]
    public async Task RangeChangesAndClear_LeaveSaveAndTransactionOwnershipWithCaller()
    {
        var first = new TestEntity { Id = Guid.NewGuid(), Name = "First" };
        var second = new TestEntity { Id = Guid.NewGuid(), Name = "Second" };
        await using (var seed = PostgreSqlTestContext.Create(postgreSql.ConnectionString))
        {
            seed.AddRange(first, second);
            await seed.SaveChangesAsync();
        }

        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var repository = new GenericRepository<TestEntity>(context, NullLogger<GenericRepository<TestEntity>>.Instance);
        IUnitOfWork unitOfWork = new UnitOfWork<CoreTestDbContext>(context, NullLogger<UnitOfWork<CoreTestDbContext>>.Instance);
        first.Name = "Changed";
        repository.UpdateRange([first]);
        repository.RemoveRange([second]);
        await repository.AddAsync(new TestEntity { Id = Guid.NewGuid(), Name = "Pending" });

        // An independent context observes only committed rows; range methods must not hide a save boundary.
        await using var observer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.Equal(new[] { "First", "Second" }, await observer.Entities.OrderBy(entity => entity.Name).Select(entity => entity.Name).ToArrayAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        unitOfWork.ClearTrackedChanges();

        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Same(transaction, context.Database.CurrentTransaction);
        Assert.Equal(0, await unitOfWork.SaveChangesAsync());
        Assert.Equal(2, await observer.Entities.CountAsync());
        await transaction.RollbackAsync();

        repository.UpdateRange([first]);
        repository.RemoveRange([second]);
        await unitOfWork.SaveChangesAsync();

        Assert.Equal("Changed", await observer.Entities.Select(entity => entity.Name).SingleAsync());
    }

    /// <summary>
    /// Extracts only SQLSTATE-classified unique violations, including the exception EF wraps on a real save.
    /// </summary>
    /// <returns>The PostgreSQL unique-constraint classification assertions.</returns>
    [Fact]
    public async Task UniqueConstraintHelper_ClassifiesRealEfFailureWithoutParsingMessages()
    {
        var key = Guid.NewGuid();
        await using (var seed = PostgreSqlTestContext.Create(postgreSql.ConnectionString))
        {
            seed.Add(new TestEntity { Id = key, Name = "Existing" });
            await seed.SaveChangesAsync();
        }

        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        context.Add(new TestEntity { Id = key, Name = "Duplicate" });
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.Equal("PK_CoreEntity", PostgreSqlExceptionHelper.GetUniqueConstraintName(exception));
        Assert.Equal("PK_CoreEntity", PostgreSqlExceptionHelper.GetUniqueConstraintName(exception.InnerException));
        Assert.Null(PostgreSqlExceptionHelper.GetUniqueConstraintName(null));
        Assert.Null(PostgreSqlExceptionHelper.GetUniqueConstraintName(new Exception("duplicate key PK_CoreEntity")));
        Assert.Null(PostgreSqlExceptionHelper.GetUniqueConstraintName(new Npgsql.PostgresException("duplicate", "ERROR", "ERROR", "23502")));
    }

    /// <summary>
    /// Represents a typed projection independent of the tracked persistence entity.
    /// </summary>
    /// <param name="Name">The single selected column.</param>
    private sealed record NameProjection(string Name);

    /// <summary>
    /// Verifies that repository phase logs omit entity identifiers and property values.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task RepositoryLogs_DescribePhasesWithoutEntityValuesOrIdentifiers()
    {
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var logger = new TestLogger<GenericRepository<TestEntity>>();
        var repository = new GenericRepository<TestEntity>(context, logger);
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "private-entity-value" };
        await repository.AddAsync(entity);
        await context.SaveChangesAsync();
        await repository.FirstOrDefaultAsync(predicate: row => row.Id == entity.Id);
        await repository.GetListAsync();

        Assert.Contains(logger.Entries, entry => entry.Message.Contains("staged", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, entry => entry.Message.StartsWith(
            "Repository query FirstOrDefaultAsync started", StringComparison.Ordinal));
        Assert.Contains(
            logger.Entries,
            entry => entry.Message.StartsWith("Repository query GetListAsync completed", StringComparison.Ordinal));
        var output = string.Join('\n', logger.Entries.Select(entry => entry.Message));
        Assert.DoesNotContain(entity.Id.ToString(), output);
        Assert.DoesNotContain(entity.Name, output);
        Assert.All(logger.Entries, entry => Assert.Null(entry.Exception));
    }

    /// <summary>
    /// Verifies that adding single or multiple entities changes tracking state without an implicit save.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task AddAndAddRangeOnlyStageEntitiesUntilSaveChanges()
    {
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var repository = new GenericRepository<TestEntity>(context, NullLogger<GenericRepository<TestEntity>>.Instance);

        await repository.AddAsync(new TestEntity { Id = Guid.NewGuid(), Name = "One" });
        await repository.AddRangeAsync(
        [
            new TestEntity { Id = Guid.NewGuid(), Name = "Two" },
            new TestEntity { Id = Guid.NewGuid(), Name = "Three" }
        ]);

        await using (var beforeSave = PostgreSqlTestContext.Create(postgreSql.ConnectionString))
        {
            Assert.Equal(0, await beforeSave.Entities.CountAsync());
        }

        await context.SaveChangesAsync();

        await using var afterSave = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.Equal(3, await afterSave.Entities.CountAsync());
    }

    /// <summary>
    /// Verifies that a detached entity update is persisted only after the caller saves.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task UpdateWorksForDetachedEntityWithoutSavingAutomatically()
    {
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "Before" };
        await using (var seed = PostgreSqlTestContext.Create(postgreSql.ConnectionString))
        {
            seed.Add(entity);
            await seed.SaveChangesAsync();
        }

        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var repository = new GenericRepository<TestEntity>(context, NullLogger<GenericRepository<TestEntity>>.Instance);
        entity.Name = "After";

        repository.Update(entity);

        await using (var beforeSave = PostgreSqlTestContext.Create(postgreSql.ConnectionString))
        {
            Assert.Equal("Before", (await beforeSave.Entities.SingleAsync()).Name);
        }

        await context.SaveChangesAsync();

        await using var afterSave = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.Equal("After", (await afterSave.Entities.SingleAsync()).Name);
    }

    /// <summary>
    /// Verifies that removal remains staged until the caller explicitly saves changes.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task RemoveDoesNotSaveAutomatically()
    {
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "Remove" };
        await using (var seed = PostgreSqlTestContext.Create(postgreSql.ConnectionString))
        {
            seed.Add(entity);
            await seed.SaveChangesAsync();
        }

        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var repository = new GenericRepository<TestEntity>(context, NullLogger<GenericRepository<TestEntity>>.Instance);
        repository.Remove(entity);

        await using (var beforeSave = PostgreSqlTestContext.Create(postgreSql.ConnectionString))
        {
            Assert.True(await beforeSave.Entities.AnyAsync());
        }

        await context.SaveChangesAsync();

        await using var afterSave = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.False(await afterSave.Entities.AnyAsync());
    }

    /// <summary>
    /// Reads a persisted key match through the predicate pipeline using native EF tracking.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task PredicateLookupReturnsTrackedDatabaseMatchOrNull()
    {
        var first = new TestEntity { Id = Guid.NewGuid(), Name = "Match" };
        await using (var seed = PostgreSqlTestContext.Create(postgreSql.ConnectionString))
        {
            seed.AddRange(first, new TestEntity { Id = Guid.NewGuid(), Name = "Other" });
            await seed.SaveChangesAsync();
        }

        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var repository = new GenericRepository<TestEntity>(context, NullLogger<GenericRepository<TestEntity>>.Instance);

        var missingId = Guid.NewGuid();
        var found = await repository.FirstOrDefaultAsync(predicate: row => row.Id == first.Id);
        var missing = await repository.FirstOrDefaultAsync(predicate: row => row.Id == missingId);

        Assert.NotNull(found);
        Assert.Equal(first.Id, found.Id);
        Assert.Null(missing);
        Assert.Same(found, Assert.Single(context.ChangeTracker.Entries<TestEntity>()).Entity);
    }

    /// <summary>
    /// Keeps the list contract read-only while its entity instances use native EF tracking.
    /// </summary>
    /// <returns>The filtered collection and tracked entity assertions.</returns>
    [Fact]
    public async Task ListReturnsReadOnlyCollectionOfTrackedEntities()
    {
        await using (var seed = PostgreSqlTestContext.Create(postgreSql.ConnectionString))
        {
            seed.AddRange(new TestEntity { Id = Guid.NewGuid(), Name = "Match" },
                new TestEntity { Id = Guid.NewGuid(), Name = "Other" });
            await seed.SaveChangesAsync();
        }

        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var repository = new GenericRepository<TestEntity>(context, NullLogger<GenericRepository<TestEntity>>.Instance);

        var list = await repository.GetListAsync(entity => entity.Name == "Match");

        Assert.Single(list);
        Assert.IsAssignableFrom<IReadOnlyList<TestEntity>>(list);
        Assert.Same(list[0], Assert.Single(context.ChangeTracker.Entries<TestEntity>()).Entity);
    }

    /// <summary>
    /// Queries persisted rows while preserving native identity resolution and pending edits, including on cancellation.
    /// </summary>
    /// <param name="cancelled">Whether the predicate read receives an already-cancelled token.</param>
    /// <returns>The independent database read, staged change and cancellation assertions.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PredicateLookupDoesNotReplaceTrackedChanges(bool cancelled)
    {
        var id = Guid.NewGuid();
        await using (var seed = PostgreSqlTestContext.Create(postgreSql.ConnectionString))
        {
            seed.Add(new TestEntity { Id = id, Name = "Committed" });
            await seed.SaveChangesAsync();
        }

        var commands = new List<string>();
        var options = new DbContextOptionsBuilder<CoreTestDbContext>().UseNpgsql(postgreSql.ConnectionString)
            .LogTo(commands.Add, [Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuted]).Options;
        await using var context = new CoreTestDbContext(options);
        var repository = new GenericRepository<TestEntity>(context, NullLogger<GenericRepository<TestEntity>>.Instance);
        var tracked = await context.Entities.SingleAsync(entity => entity.Id == id);
        tracked.Name = "Pending edit";
        context.ChangeTracker.DetectChanges();
        var commandCount = commands.Count;
        using var cancellation = new CancellationTokenSource();
        if (cancelled)
        {
            cancellation.Cancel();
        }

        // A read always uses the database pipeline, even when the context already tracks the same key.
        // Cancellation must not detach that tracked instance or discard its pending edits.
        if (cancelled)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.FirstOrDefaultAsync(
                predicate: row => row.Id == id, cancellationToken: cancellation.Token));

            Assert.Equal(commandCount, commands.Count);
        }
        else
        {
            var result = await repository.FirstOrDefaultAsync(predicate: row => row.Id == id);

            Assert.NotNull(result);
            Assert.Same(tracked, result);
            Assert.Equal("Pending edit", result.Name);
            Assert.Equal(commandCount + 1, commands.Count);
        }

        Assert.Same(tracked, Assert.Single(context.ChangeTracker.Entries<TestEntity>()).Entity);
        Assert.Equal("Pending edit", tracked.Name);
        Assert.Equal(EntityState.Modified, context.Entry(tracked).State);
        Assert.True(commandCount > 0);
        Assert.Equal(0, context.SaveCallCount);
        await using var observer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.Equal("Committed", (await observer.Entities.SingleAsync()).Name);
    }

    /// <summary>
    /// Leaves an unsaved entity staged while a predicate read correctly finds no persisted row.
    /// </summary>
    /// <returns>The local instance, added state and independent database assertions.</returns>
    [Fact]
    public async Task PredicateLookupDoesNotReturnAnUnsavedEntity()
    {
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var repository = new GenericRepository<TestEntity>(context, NullLogger<GenericRepository<TestEntity>>.Instance);
        var staged = await repository.AddAsync(new TestEntity { Id = Guid.NewGuid(), Name = "Pending" });

        var result = await repository.FirstOrDefaultAsync(predicate: row => row.Id == staged.Id);

        Assert.Null(result);
        Assert.Same(staged, Assert.Single(context.ChangeTracker.Entries<TestEntity>()).Entity);
        Assert.Equal(EntityState.Added, context.Entry(staged).State);
        Assert.Equal(0, context.SaveCallCount);
        await using var observer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.False(await observer.Entities.AnyAsync());
    }

    /// <summary>
    /// Propagates cancellation through a predicate read without introducing tracked entities.
    /// </summary>
    /// <returns>The cancellation and empty tracker assertions.</returns>
    [Fact]
    public async Task PredicateLookupHonorsCancellation()
    {
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var repository = new GenericRepository<TestEntity>(context, NullLogger<GenericRepository<TestEntity>>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var id = Guid.NewGuid();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.FirstOrDefaultAsync(
            predicate: row => row.Id == id, cancellationToken: cancellation.Token));

        Assert.Empty(context.ChangeTracker.Entries<TestEntity>());
    }

    /// <summary>
    /// Returns a tracked match or null without treating multiple matches as a uniqueness guarantee.
    /// </summary>
    /// <param name="matchCount">The number of persisted rows satisfying the predicate.</param>
    /// <returns>The PostgreSQL lookup and tracking assertions.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task FirstOrDefaultUsesNativeTrackingWithoutRequiringUniqueness(int matchCount)
    {
        var matches = Enumerable.Range(0, matchCount)
            .Select(_ => new TestEntity { Id = Guid.NewGuid(), Name = "Match" })
            .ToArray();
        await using (var seed = PostgreSqlTestContext.Create(postgreSql.ConnectionString))
        {
            seed.AddRange(matches);
            seed.Add(new TestEntity { Id = Guid.NewGuid(), Name = "Other" });
            await seed.SaveChangesAsync();
        }

        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        IGenericRepository<TestEntity> repository = new GenericRepository<TestEntity>(context, NullLogger<GenericRepository<TestEntity>>.Instance);

        var result = await repository.FirstOrDefaultAsync(entity => entity.Name == "Match");

        if (matchCount == 0)
        {
            Assert.Null(result);
        }
        else
        {
            // Either matching row is valid; this shared API deliberately promises no ordering.
            Assert.NotNull(result);
            Assert.Contains(matches, entity => entity.Id == result.Id);
        }

        Assert.Equal(matchCount == 0 ? 0 : 1, context.ChangeTracker.Entries<TestEntity>().Count());
    }

    /// <summary>
    /// Leaves staged writes uncommitted when a caller performs a shared single-entity read.
    /// </summary>
    /// <returns>The asynchronous tracker and independent PostgreSQL context assertions.</returns>
    [Fact]
    public async Task FirstOrDefaultDoesNotSaveStagedChanges()
    {
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var repository = new GenericRepository<TestEntity>(context, NullLogger<GenericRepository<TestEntity>>.Instance);
        await repository.AddAsync(new TestEntity { Id = Guid.NewGuid(), Name = "Pending" });

        var result = await repository.FirstOrDefaultAsync(entity => entity.Name == "Pending");

        Assert.Null(result);
        Assert.Equal(EntityState.Added, Assert.Single(context.ChangeTracker.Entries<TestEntity>()).State);
        await using var observer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.False(await observer.Entities.AnyAsync());
    }

    /// <summary>
    /// Uses the entire entity set when callers omit a filter or pass null explicitly.
    /// </summary>
    /// <param name="rowCount">The number of persisted entities in the test database.</param>
    /// <returns>The asynchronous unfiltered lookup and tracking assertions.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task OptionalPredicatesAllowUnfilteredRepositoryReads(int rowCount)
    {
        await using (var seed = PostgreSqlTestContext.Create(postgreSql.ConnectionString))
        {
            seed.AddRange(Enumerable.Range(0, rowCount).Select(_ => new TestEntity { Id = Guid.NewGuid(), Name = "Existing" }));
            await seed.SaveChangesAsync();
        }

        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        IGenericRepository<TestEntity> repository = new GenericRepository<TestEntity>(context, NullLogger<GenericRepository<TestEntity>>.Instance);

        Assert.Equal(rowCount > 0, await repository.AnyAsync());
        Assert.Equal(rowCount > 0, await repository.AnyAsync(null));
        Assert.Equal(rowCount == 0, await repository.FirstOrDefaultAsync() is null);
        Assert.Equal(rowCount == 0, await repository.FirstOrDefaultAsync(null) is null);
        Assert.Equal(rowCount, (await repository.GetListAsync()).Count);
        Assert.Equal(rowCount, (await repository.GetListAsync(null)).Count);
        Assert.Equal(rowCount, context.ChangeTracker.Entries<TestEntity>().Count());
    }

    /// <summary>
    /// Preserves native cancellation across filtered, omitted and explicitly null read predicates.
    /// </summary>
    /// <returns>The asynchronous argument and cancellation assertions.</returns>
    [Fact]
    public async Task FirstOrDefaultHonorsCancellationWithAndWithoutPredicate()
    {
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var repository = new GenericRepository<TestEntity>(context, NullLogger<GenericRepository<TestEntity>>.Instance);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.FirstOrDefaultAsync(entity => entity.Name == "Match", cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.FirstOrDefaultAsync(cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.AnyAsync(cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.FirstOrDefaultAsync(null, cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.AnyAsync(null, cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.GetListAsync(entity => entity.Name == "Match", cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.GetListAsync(cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.GetListAsync(null, cancellationToken: cancellation.Token));

        Assert.Empty(context.ChangeTracker.Entries<TestEntity>());
    }

    /// <summary>
    /// Verifies existence predicates and propagation of an already-cancelled token.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task AnyUsesPredicateAndHonorsCancellation()
    {
        await using (var seed = PostgreSqlTestContext.Create(postgreSql.ConnectionString))
        {
            seed.Add(new TestEntity { Id = Guid.NewGuid(), Name = "Exists" });
            await seed.SaveChangesAsync();
        }

        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var repository = new GenericRepository<TestEntity>(context, NullLogger<GenericRepository<TestEntity>>.Instance);

        Assert.True(await repository.AnyAsync(entity => entity.Name == "Exists"));
        Assert.False(await repository.AnyAsync(entity => entity.Name == "Missing"));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.AnyAsync(entity => entity.Name == "Exists", cancellationToken: cancellation.Token));
    }
}

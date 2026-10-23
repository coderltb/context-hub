namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Persistence.Dapper;

/// <summary>
/// Verifies native buffered JOIN mapping, resource ownership and cancellation against PostgreSQL.
/// </summary>
/// <param name="postgreSql">The collection-owned disposable database.</param>
[Collection(PostgreSqlCollection.Name)]
public sealed class DapperMultiMappingTests(PostgreSqlFixture postgreSql)
{
    /// <summary>
    /// Splits joined rows natively and uses the existing global profile for the returned DTO alias.
    /// </summary>
    /// <param name="inputs">The number of row types split by Dapper.</param>
    /// <returns>The DTO, buffered-result and connection-release assertions.</returns>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task JoinedRowsUseGlobalMappingAndReleaseOwnedConnections(int inputs)
    {
        new ServiceCollection().AddMapping();
        await using var source = NpgsqlDataSource.Create(postgreSql.ConnectionString);
        var factory = new DapperPostgreSqlTests.TrackingFactory(source);
        IDapperService service = new DapperService(factory, NullLogger<DapperService>.Instance);
        var sql = "select @parent as \"Name\", 2 as \"Id\", 'Child' as \"Name\"";

        // The service only splits one SQL row; feature mapping remains a callback using startup profiles.
        var rows = inputs == 2
            ? await service.QueryAsync<Mappings.GlobalMappingSource, JoinRow, JoinedResult>(sql,
                (parent, child) => MapRows(parent, child), "Id", new { parent = "Parent" })
            : await service.QueryAsync<Mappings.GlobalMappingSource, JoinRow, JoinRow, JoinedResult>(
                sql + ", 3 as \"Id\", 'Leaf' as \"Name\"", MapRows, "Id,Id", new { parent = "Parent" });

        Assert.Equal(System.Data.ConnectionState.Closed, factory.LastConnection!.State);
        var row = Assert.Single(rows);
        Assert.Equal("Parent", row.DisplayName);
        Assert.Equal("Child", row.ChildName);
        Assert.Equal(inputs == 3 ? "Leaf" : null, row.LeafName);
    }

    /// <summary>
    /// Preserves Dapper's null object for an absent LEFT JOIN instead of inventing a child instance.
    /// </summary>
    /// <returns>The parent DTO and missing child assertions.</returns>
    [Fact]
    public async Task LeftJoinAllowsMissingChild()
    {
        new ServiceCollection().AddMapping();
        await using var source = NpgsqlDataSource.Create(postgreSql.ConnectionString);
        var factory = new DapperPostgreSqlTests.TrackingFactory(source);
        var service = new DapperService(factory, NullLogger<DapperService>.Instance);
        JoinRow? observedChild = new JoinRow();

        var rows = await service.QueryAsync<Mappings.GlobalMappingSource, JoinRow?, JoinedResult>(
            "select 'Parent' as \"Name\", null::int as \"Id\", null::text as \"Name\"",
            (parent, child) =>
            {
                observedChild = child;

                return MapRows(parent, child);
            }, "Id");

        Assert.Null(observedChild);
        Assert.Null(Assert.Single(rows).ChildName);
        Assert.Equal("Parent", rows[0].DisplayName);
        Assert.Equal(System.Data.ConnectionState.Closed, factory.LastConnection!.State);
    }

    /// <summary>
    /// Preserves callback failures and releases the owned connection for either multi-map overload.
    /// </summary>
    /// <param name="inputs">The number of mapped input row types.</param>
    /// <returns>The original failure identity and disposal assertions.</returns>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task CallbackFailureIsNotWrapped(int inputs)
    {
        await using var source = NpgsqlDataSource.Create(postgreSql.ConnectionString);
        var factory = new DapperPostgreSqlTests.TrackingFactory(source);
        var service = new DapperService(factory, NullLogger<DapperService>.Instance);
        var failure = new IOException("Fixture mapping failure");
        var sql = "select 1 as \"Id\", 'Parent' as \"Name\", 2 as \"Id\", 'Child' as \"Name\"";

        var actual = await Assert.ThrowsAsync<IOException>(() => inputs == 2
            ? service.QueryAsync<JoinRow, JoinRow, string>(sql, (_, _) => throw failure, "Id")
            : service.QueryAsync<JoinRow, JoinRow, JoinRow, string>(sql + ", 3 as \"Id\", 'Leaf' as \"Name\"",
                (_, _, _) => throw failure, "Id,Id"));

        Assert.Same(failure, actual);
        Assert.Equal(System.Data.ConnectionState.Closed, factory.LastConnection!.State);
    }

    /// <summary>
    /// Leaves invalid column splitting to native Dapper while ensuring resources are released.
    /// </summary>
    /// <returns>The native split error and disposal assertions.</returns>
    [Fact]
    public async Task UnknownSplitColumnFailsNatively()
    {
        await using var source = NpgsqlDataSource.Create(postgreSql.ConnectionString);
        var factory = new DapperPostgreSqlTests.TrackingFactory(source);
        var service = new DapperService(factory, NullLogger<DapperService>.Instance);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.QueryAsync<JoinRow, JoinRow, string>(
            "select 1 as \"Id\", 'Parent' as \"Name\"", (parent, child) => parent.Name, "MissingColumn"));

        Assert.Equal(System.Data.ConnectionState.Closed, factory.LastConnection!.State);
    }

    /// <summary>
    /// Passes cancellation and timeout through the shared CommandDefinition for both overloads.
    /// </summary>
    /// <param name="inputs">The number of mapped input types.</param>
    /// <param name="timeout">Whether to test provider timeout rather than caller cancellation.</param>
    /// <returns>The native failure and released-connection assertions.</returns>
    [Theory]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public async Task MultiMappingForwardsCancellationAndTimeout(int inputs, bool timeout)
    {
        await using var source = NpgsqlDataSource.Create(postgreSql.ConnectionString);
        var factory = new DapperPostgreSqlTests.TrackingFactory(source);
        var service = new DapperService(factory, NullLogger<DapperService>.Instance);
        using var cancellation = new CancellationTokenSource();
        if (!timeout)
        {
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(200));
        }

        var options = new DapperCommandOptions { CommandTimeout = timeout ? 1 : 30, CancellationToken = cancellation.Token };
        var sql = "select 1 as \"Id\", 'Parent' as \"Name\", 2 as \"Id\", 'Child' as \"Name\"";
        var command = inputs == 2
            ? service.QueryAsync<JoinRow, JoinRow, string>(sql + " from pg_sleep(5)", (parent, child) => parent.Name, "Id", options: options)
            : service.QueryAsync<JoinRow, JoinRow, JoinRow, string>(sql + ", 3 as \"Id\", 'Leaf' as \"Name\" from pg_sleep(5)",
                (parent, child, leaf) => parent.Name, "Id,Id", options: options);

        if (timeout)
        {
            await Assert.ThrowsAnyAsync<NpgsqlException>(() => command);
        }
        else
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command);
        }

        Assert.Equal(System.Data.ConnectionState.Closed, factory.LastConnection!.State);
    }

    /// <summary>
    /// Keeps a borrowed EF connection and transaction owned by their caller after successful and failed mapping.
    /// </summary>
    /// <returns>The acquisition, rollback ownership and open-connection assertions.</returns>
    [Fact]
    public async Task MultiMappingBorrowsEfTransactionWithoutCompletingIt()
    {
        await using var source = NpgsqlDataSource.Create(postgreSql.ConnectionString);
        var factory = new DapperPostgreSqlTests.TrackingFactory(source);
        var service = new DapperService(factory, NullLogger<DapperService>.Instance);
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        await context.Database.OpenConnectionAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var options = DapperCommandOptionsHelper.CreateTransactionalText(context);
        var sql = "select 1 as \"Id\", 'Parent' as \"Name\", 2 as \"Id\", 'Child' as \"Name\"";

        Assert.Equal("Parent", Assert.Single(await service.QueryAsync<JoinRow, JoinRow, string>(
            sql, (parent, child) => parent.Name, "Id", options: options)));
        Assert.Equal("Child", Assert.Single(await service.QueryAsync<JoinRow, JoinRow, JoinRow, string>(
            sql + ", 3 as \"Id\", 'Leaf' as \"Name\"", (parent, child, leaf) => child.Name, "Id,Id", options: options)));

        // A provider failure aborts this transaction, but only its owner may roll it back.
        var failure = await Assert.ThrowsAsync<PostgresException>(() => service.QueryAsync<JoinRow, JoinRow, string>(
            "select 1 / 0 as \"Id\", 'Parent' as \"Name\", 2 as \"Id\", 'Child' as \"Name\"",
            (parent, child) => parent.Name, "Id", options: options));

        Assert.Equal(PostgresErrorCodes.DivisionByZero, failure.SqlState);
        Assert.Same(transaction, context.Database.CurrentTransaction);
        Assert.Equal(0, factory.Calls);
        Assert.Equal(System.Data.ConnectionState.Open, context.Database.GetDbConnection().State);
        await transaction.RollbackAsync();
        Assert.Equal(1, await context.Database.GetDbConnection().ExecuteScalarAsync<int>("select 1"));
    }

    /// <summary>
    /// Uses a feature mapping profile for DTO aliases after Dapper has split the SQL row.
    /// </summary>
    /// <param name="parent">The row shape supplied to the existing Mapster profile.</param>
    /// <param name="child">The nullable joined child row.</param>
    /// <param name="leaf">The nullable third joined row.</param>
    /// <returns>A feature-owned materialized JOIN result.</returns>
    private static JoinedResult MapRows(Mappings.GlobalMappingSource parent, JoinRow? child, JoinRow? leaf = null) =>
        new(parent.Adapt<Mappings.GlobalMappingTarget>().DisplayName, child?.Name, leaf?.Name);

    /// <summary>
    /// Supplies a simple native Dapper row shape for JOIN splitting.
    /// </summary>
    public sealed class JoinRow
    {
        /// <summary>
        /// Gets the split key, whose null value represents an absent joined object.
        /// </summary>
        public int Id { get; init; }

        /// <summary>
        /// Gets the ordinary provider value used by callback assertions.
        /// </summary>
        public string Name { get; init; } = string.Empty;
    }

    /// <summary>
    /// Carries only materialized values, not a reader or an open database dependency.
    /// </summary>
    /// <param name="DisplayName">The parent alias produced by the global Mapster profile.</param>
    /// <param name="ChildName">The child name, absent for a missing LEFT JOIN.</param>
    /// <param name="LeafName">The optional third row's name.</param>
    private sealed record JoinedResult(string DisplayName, string? ChildName, string? LeafName);
}

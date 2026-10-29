namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Services.Persistence;

/// <summary>
/// Injects resource-disposal failures beneath native Dapper to verify primary errors and ownership.
/// </summary>
public sealed class DapperCleanupTests
{
    /// <summary>
    /// Preserves the command failure or cancellation when owned connection disposal also fails.
    /// </summary>
    /// <param name="cancelled">Whether the original command failure is cancellation.</param>
    /// <returns>The exception-identity and disposal assertions.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommandFailure_WinsOverConnectionCleanup(bool cancelled)
    {
        Exception failure = cancelled ? new OperationCanceledException("Fixture cancellation") : new IOException("Fixture query failure");
        var connection = new CleanupConnection { QueryFailure = failure, CleanupFailure = new IOException("Fixture cleanup failure") };
        var logger = new TestLogger<DapperService>();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IDbConnectionFactory>(new CleanupFactory(connection));
        services.AddSingleton<ILogger<DapperService>>(logger);
        services.AddTransient<IDapperService, DapperService>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var service = provider.GetRequiredService<IDapperService>();

        var actual = await Record.ExceptionAsync(() => service.ExecuteAsync("fixture command"));

        Assert.Same(failure, actual);
        Assert.Equal(1, connection.Disposals);
        // Only secondary cleanup needs a local diagnostic; the command failure remains owned by the caller.
        var warning = Assert.Single(logger.Entries, entry => entry.Level >= LogLevel.Warning);
        Assert.Contains("cleanup failed", warning.Message);
        Assert.DoesNotContain("Fixture", warning.Message);
        Assert.Null(warning.Exception);
    }

    /// <summary>
    /// Propagates disposal failures when no earlier command failure exists.
    /// </summary>
    /// <returns>The successful-command cleanup exception assertion.</returns>
    [Fact]
    public async Task SuccessfulCommand_DoesNotHideConnectionCleanupFailure()
    {
        var failure = new IOException("Fixture cleanup failure");
        var connection = new CleanupConnection { CleanupFailure = failure };
        var service = new DapperService(new CleanupFactory(connection), NullLogger<DapperService>.Instance);

        Assert.Same(failure, await Record.ExceptionAsync(() => service.ExecuteAsync("fixture command")));
        Assert.Equal(1, connection.Disposals);
    }

    /// <summary>
    /// Preserves callback errors when GridReader cleanup and optionally connection cleanup fail afterwards.
    /// </summary>
    /// <param name="cancelled">Whether the callback is cancelled.</param>
    /// <param name="connectionFails">Whether connection cleanup also fails after GridReader cleanup.</param>
    /// <returns>The original exception and both attempted cleanup assertions.</returns>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CallbackFailure_WinsOverReaderAndConnectionCleanup(bool cancelled, bool connectionFails)
    {
        Exception failure = cancelled ? new OperationCanceledException("Fixture callback cancellation") : new IOException("Fixture callback failure");
        var connection = new CleanupConnection
        {
            ReaderCleanupFailure = new IOException("Fixture reader cleanup failure"),
            CleanupFailure = connectionFails ? new IOException("Fixture connection cleanup failure") : null
        };
        var service = new DapperService(new CleanupFactory(connection), NullLogger<DapperService>.Instance);

        // Native GridReader owns the provider reader and command; the callback fails before consuming its result set.
        var actual = await Record.ExceptionAsync(() => service.QueryMultipleAsync("fixture rows", _ => Task.FromException<int>(failure)));

        Assert.Same(failure, actual);
        Assert.Equal(1, connection.ReaderDisposals);
        Assert.Equal(1, connection.Disposals);
    }

    /// <summary>
    /// Reports reader cleanup failure even when the callback returned a successful result.
    /// </summary>
    /// <returns>The reader failure and attempted connection release assertions.</returns>
    [Fact]
    public async Task SuccessfulCallback_DoesNotHideReaderCleanupFailure()
    {
        var failure = new IOException("Fixture reader cleanup failure");
        var connection = new CleanupConnection { ReaderCleanupFailure = failure };
        var service = new DapperService(new CleanupFactory(connection), NullLogger<DapperService>.Instance);

        Assert.Same(failure, await Record.ExceptionAsync(() => service.QueryMultipleAsync("fixture rows", _ => Task.FromResult(7))));
        Assert.Equal(1, connection.ReaderDisposals);
        Assert.Equal(1, connection.Disposals);
    }

    /// <summary>
    /// Leaves a borrowed connection and transaction untouched when a command fails.
    /// </summary>
    /// <returns>The original command failure and zero shared-owned cleanup assertions.</returns>
    [Fact]
    public async Task BorrowedCommand_DoesNotDisposeCallerResources()
    {
        var failure = new IOException("Fixture borrowed command failure");
        var connection = new CleanupConnection { QueryFailure = failure, CleanupFailure = new IOException("Must not run") };
        var transaction = new BorrowedTransaction(connection);
        var service = new DapperService(new CleanupFactory(connection), NullLogger<DapperService>.Instance);

        var actual = await Record.ExceptionAsync(() => service.ExecuteAsync("fixture command",
            options: DapperCommandOptionsHelper.CreateText(transaction: transaction)));

        Assert.Same(failure, actual);
        Assert.Equal(0, connection.Disposals);
        Assert.False(transaction.Disposed);
    }

    /// <summary>
    /// Returns the resource under test without replacing the native Dapper facade or its command handling.
    /// </summary>
    /// <param name="connection">The already open fault-injection resource.</param>
    private sealed class CleanupFactory(CleanupConnection connection) : IDbConnectionFactory
    {
        /// <summary>
        /// Acquires the resource with the same factory contract as a pooled connection.
        /// </summary>
        /// <param name="cancellationToken">The acquisition cancellation token.</param>
        /// <returns>The test-owned open connection.</returns>
        public Task<DbConnection> GetOpenConnectionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<DbConnection>(connection);
    }

    /// <summary>
    /// Provides only the provider boundary needed to inject deterministic command and disposal failures.
    /// </summary>
    private sealed class CleanupConnection : DbConnection
    {
        /// <summary>
        /// Supplies the optional failure from native Dapper's command execution.
        /// </summary>
        public Exception? QueryFailure { get; init; }

        /// <summary>
        /// Supplies the optional owned connection disposal failure.
        /// </summary>
        public Exception? CleanupFailure { get; init; }

        /// <summary>
        /// Supplies the optional command disposal failure inside native GridReader cleanup.
        /// </summary>
        public Exception? ReaderCleanupFailure { get; init; }

        /// <summary>
        /// Counts owned connection cleanup attempts.
        /// </summary>
        public int Disposals { get; private set; }

        /// <summary>
        /// Counts provider-command cleanup attempts by GridReader.
        /// </summary>
        public int ReaderDisposals { get; set; }

        /// <summary>
        /// Holds an unused provider connection string without contacting any database.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Identifies the test-only database.
        /// </summary>
        public override string Database => "fixture";

        /// <summary>
        /// Identifies the test-only provider.
        /// </summary>
        public override string DataSource => "fixture";

        /// <summary>
        /// Supplies harmless provider version metadata.
        /// </summary>
        public override string ServerVersion => "fixture";

        /// <summary>
        /// Keeps Dapper on its already-open connection path.
        /// </summary>
        public override ConnectionState State => ConnectionState.Open;

        /// <summary>
        /// Rejects database switching outside this resource-lifetime test.
        /// </summary>
        /// <param name="databaseName">The unused requested database.</param>
        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        /// <summary>
        /// Leaves synchronous closing unused; the facade owns asynchronous disposal.
        /// </summary>
        public override void Close() { }

        /// <summary>
        /// Leaves acquisition unused because the fixture resource is already open.
        /// </summary>
        public override void Open() { }

        /// <summary>
        /// Rejects transaction creation because these tests exercise ownership, not a substitute database.
        /// </summary>
        /// <param name="isolationLevel">The unused transaction isolation.</param>
        /// <returns>No transaction because creation is unsupported.</returns>
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();

        /// <summary>
        /// Supplies a provider command for native Dapper to execute and release.
        /// </summary>
        /// <returns>The resource-bound test command.</returns>
        protected override DbCommand CreateDbCommand() => new CleanupCommand(this);

        /// <summary>
        /// Records the release attempt before injecting its secondary failure.
        /// </summary>
        /// <returns>The completed or deliberately failed disposal.</returns>
        public override ValueTask DisposeAsync()
        {
            Disposals++;

            return CleanupFailure is null ? ValueTask.CompletedTask : ValueTask.FromException(CleanupFailure);
        }
    }

    /// <summary>
    /// Uses a native DataTableReader so only provider execution and disposal are fault-injection seams.
    /// </summary>
    /// <param name="owner">The connection owning this command's failure configuration.</param>
    private sealed class CleanupCommand(CleanupConnection owner) : DbCommand
    {
        /// <summary>
        /// Reuses native Npgsql parameter handling without opening a database.
        /// </summary>
        private readonly NpgsqlCommand _parameters = new();

        /// <summary>
        /// Distinguishes GridReader disposal from ordinary execute-command disposal.
        /// </summary>
        private bool _hasReader;

        /// <summary>
        /// Stores the command text assigned by Dapper.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string CommandText { get; set; } = string.Empty;

        /// <summary>
        /// Stores Dapper's configured timeout.
        /// </summary>
        public override int CommandTimeout { get; set; }

        /// <summary>
        /// Stores the selected native command type.
        /// </summary>
        public override System.Data.CommandType CommandType { get; set; }

        /// <summary>
        /// Stores the provider's design-time visibility flag.
        /// </summary>
        public override bool DesignTimeVisible { get; set; }

        /// <summary>
        /// Stores the native row-update policy.
        /// </summary>
        public override System.Data.UpdateRowSource UpdatedRowSource { get; set; }

        /// <summary>
        /// Retains the connection assigned by native Dapper.
        /// </summary>
        protected override DbConnection? DbConnection { get; set; } = owner;

        /// <summary>
        /// Retains any provider transaction assigned to the command.
        /// </summary>
        protected override DbTransaction? DbTransaction { get; set; }

        /// <summary>
        /// Supplies the native parameter collection.
        /// </summary>
        protected override DbParameterCollection DbParameterCollection => _parameters.Parameters;

        /// <summary>
        /// Leaves provider cancellation unmodified by the cleanup fixture.
        /// </summary>
        public override void Cancel() { }

        /// <summary>
        /// Leaves command preparation unmodified by the cleanup fixture.
        /// </summary>
        public override void Prepare() { }

        /// <summary>
        /// Executes the configured command failure or a successful affected-row result.
        /// </summary>
        /// <returns>One affected row on success.</returns>
        public override int ExecuteNonQuery() => owner.QueryFailure is null ? 1 : throw owner.QueryFailure;

        /// <summary>
        /// Preserves the same deterministic command outcome through Dapper's async path.
        /// </summary>
        /// <param name="cancellationToken">The native command cancellation token.</param>
        /// <returns>The successful row count or original injected failure.</returns>
        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) =>
            owner.QueryFailure is null ? Task.FromResult(1) : Task.FromException<int>(owner.QueryFailure);

        /// <summary>
        /// Provides the scalar outcome for the native command surface.
        /// </summary>
        /// <returns>The fixture scalar value.</returns>
        public override object ExecuteScalar() => ExecuteNonQuery();

        /// <summary>
        /// Creates a native provider parameter when Dapper needs one.
        /// </summary>
        /// <returns>A native PostgreSQL parameter.</returns>
        protected override DbParameter CreateDbParameter() => new NpgsqlParameter();

        /// <summary>
        /// Returns a real BCL reader consumed and disposed by native Dapper GridReader.
        /// </summary>
        /// <param name="behavior">The native reader command behavior.</param>
        /// <returns>The fixture's one-row reader.</returns>
        protected override DbDataReader ExecuteDbDataReader(System.Data.CommandBehavior behavior)
        {
            _hasReader = true;
            var table = new System.Data.DataTable();
            table.Columns.Add("Value", typeof(int));
            table.Rows.Add(1);

            return table.CreateDataReader();
        }

        /// <summary>
        /// Releases provider state before optionally failing inside GridReader cleanup.
        /// </summary>
        /// <param name="disposing">Whether the native command is releasing managed resources.</param>
        protected override void Dispose(bool disposing)
        {
            // Component finalizers also call this overload with false; fault injection belongs only to explicit cleanup.
            if (disposing)
            {
                _parameters.Dispose();
                if (_hasReader)
                {
                    owner.ReaderDisposals++;
                    if (owner.ReaderCleanupFailure is not null)
                    {
                        throw owner.ReaderCleanupFailure;
                    }
                }
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Models a caller-owned transaction without letting the facade commit or release it.
    /// </summary>
    /// <param name="connection">The borrowed resource returned by the transaction.</param>
    private sealed class BorrowedTransaction(CleanupConnection connection) : DbTransaction
    {
        /// <summary>
        /// Exposes whether shared code incorrectly disposed the caller's transaction.
        /// </summary>
        public bool Disposed { get; private set; }

        /// <summary>
        /// Returns the already-open caller-owned resource.
        /// </summary>
        protected override DbConnection DbConnection => connection;

        /// <summary>
        /// Supplies harmless transaction metadata.
        /// </summary>
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        /// <summary>
        /// Rejects an unexpected commit by the Dapper facade.
        /// </summary>
        public override void Commit() => throw new InvalidOperationException("Caller owns commit.");

        /// <summary>
        /// Rejects an unexpected rollback by the Dapper facade.
        /// </summary>
        public override void Rollback() => throw new InvalidOperationException("Caller owns rollback.");

        /// <summary>
        /// Records an incorrect shared-owned release.
        /// </summary>
        /// <param name="disposing">Whether the native transaction is releasing managed resources.</param>
        protected override void Dispose(bool disposing) => Disposed = true;
    }
}

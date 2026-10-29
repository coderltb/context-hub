namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Services.Persistence;

/// <summary>
/// Checks facade guards without using a substitute database provider.
/// </summary>
public sealed class DapperServiceTests
{
    /// <summary>
    /// Rejects missing multi-map callbacks or split names before acquiring database resources.
    /// </summary>
    /// <returns>The required-input and cancelled-command assertions.</returns>
    [Fact]
    public async Task MultiMappingGuardsRunBeforeConnectionAcquisition()
    {
        var factory = new RejectingFactory();
        var service = new DapperService(factory, NullLogger<DapperService>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.QueryAsync<int, int, int>("select 1", null!, "Id"));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.QueryAsync<int, int, int, int>("select 1", null!, "Id,Id"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.QueryAsync<int, int, int>("select 1", (value, _) => value, " "));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.QueryAsync<int, int, int, int>("select 1", (value, _, _) => value, null!));
        var options = DapperCommandOptionsHelper.CreateText(new CancellationToken(true));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.QueryAsync<int, int, int>(
            "select 1", (value, _) => value, "Id", options: options));
        Assert.Equal(0, factory.Calls);
    }

    /// <summary>
    /// Rejects a missing multiple-result consumer before acquiring a connection or executing SQL.
    /// </summary>
    /// <returns>The asynchronous callback guard assertions.</returns>
    [Fact]
    public async Task QueryMultiple_NullCallbackDoesNotOpenConnection()
    {
        var factory = new RejectingFactory();
        var service = new DapperService(factory, NullLogger<DapperService>.Instance);

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => service.QueryMultipleAsync<int>("select 1", null!));

        Assert.Equal("map", exception.ParamName);
        Assert.Equal(0, factory.Calls);
    }

    /// <summary>
    /// Stops cancellation before opening a connection or running a callback.
    /// </summary>
    /// <returns>The asynchronous guard assertion.</returns>
    [Fact]
    public async Task CancelledCommand_DoesNotOpenConnection()
    {
        var factory = new RejectingFactory();
        var service = new DapperService(factory, NullLogger<DapperService>.Instance);
        var options = DapperCommandOptionsHelper.CreateText(new CancellationToken(true));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.QueryAsync<int>("select 1", options: options));
        Assert.Equal(0, factory.Calls);
    }

    /// <summary>
    /// Verifies stable defaults and rejects invalid SQL and negative timeouts before opening.
    /// </summary>
    /// <returns>The asynchronous guard assertions.</returns>
    [Fact]
    public async Task DefaultsAndGuards_AreExplicit()
    {
        var options = new DapperCommandOptions();
        Assert.Equal(System.Data.CommandType.Text, options.CommandType);
        Assert.Equal(30, options.CommandTimeout);

        var factory = new RejectingFactory();
        var service = new DapperService(factory, NullLogger<DapperService>.Instance);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => service.ExecuteAsync(" "));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ExecuteAsync("select 1", options: options with { CommandTimeout = -1 }));
        Assert.Equal(0, factory.Calls);
    }

    /// <summary>
    /// Exposes opening attempts while refusing all database access in guard tests.
    /// </summary>
    private sealed class RejectingFactory : IDbConnectionFactory
    {
        /// <summary>
        /// Counts connection acquisition attempts.
        /// </summary>
        public int Calls { get; private set; }

        /// <summary>
        /// Fails if a command guard accidentally allows database acquisition.
        /// </summary>
        /// <param name="cancellationToken">The forwarded token.</param>
        /// <returns>A failed acquisition task.</returns>
        public Task<DbConnection> GetOpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            Calls++;

            return Task.FromException<DbConnection>(new InvalidOperationException("Unexpected database access."));
        }
    }
}

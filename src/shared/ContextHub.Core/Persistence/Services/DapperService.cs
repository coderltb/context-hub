namespace ContextHub.Core.Persistence.Services;

/// <summary>
/// Preserves cardinality and connection ownership while dispatching native Dapper commands.
/// </summary>
public sealed class DapperService : IDapperService
{
    /// <summary>
    /// Opens connections only for commands that do not borrow a transaction.
    /// </summary>
    private readonly IDbConnectionFactory _factory;

    /// <summary>
    /// Records command lifecycle metadata without SQL or parameter values.
    /// </summary>
    private readonly ILogger<DapperService> _logger;

    /// <summary>
    /// Creates the facade from DI-owned dependencies.
    /// </summary>
    /// <param name="factory">The PostgreSQL connection factory.</param>
    /// <param name="logger">The metadata-only diagnostic logger.</param>
    public DapperService(IDbConnectionFactory factory, ILogger<DapperService> logger)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(logger);

        _factory = factory;
        _logger = logger;
    }

    /// <summary>
    /// Returns all rows, materialized before an owned connection is released.
    /// </summary>
    /// <typeparam name="T">The mapped result type.</typeparam>
    /// <param name="sql">Service-owned parameterized SQL, never consumer-supplied SQL.</param>
    /// <param name="param">The optional bound parameter object; never logged.</param>
    /// <param name="options">Timeout, cancellation and optional caller-owned transaction.</param>
    /// <returns>A task containing the mapped result.</returns>
    public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? param = null, DapperCommandOptions? options = null)
    {
        // Dapper buffers this command; AsList reuses that buffer before the owned connection is disposed.
        return WithCommandAsync(sql, param, options, nameof(QueryAsync), async (connection, definition) =>
            (IReadOnlyList<T>)(await connection.QueryAsync<T>(definition)).AsList());
    }

    /// <summary>
    /// Splits each buffered row into two objects and maps them before releasing an owned connection.
    /// </summary>
    /// <typeparam name="TFirst">The first row segment type.</typeparam>
    /// <typeparam name="TSecond">The second row segment type; may be nullable for a LEFT JOIN.</typeparam>
    /// <typeparam name="TResult">The materialized callback result type.</typeparam>
    /// <param name="sql">Service-owned parameterized SQL, never logged.</param>
    /// <param name="map">The required feature callback, which may reuse global Mapster profiles.</param>
    /// <param name="splitOn">The required column where the second row segment starts.</param>
    /// <param name="param">Optional bound parameters, never logged.</param>
    /// <param name="options">Timeout, cancellation and optional caller-owned transaction.</param>
    /// <returns>The fully buffered mapped rows.</returns>
    public Task<IReadOnlyList<TResult>> QueryAsync<TFirst, TSecond, TResult>(string sql,
        Func<TFirst, TSecond, TResult> map, string splitOn, object? param = null, DapperCommandOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentException.ThrowIfNullOrWhiteSpace(splitOn);

        // Native CommandDefinition preserves cancellation and transaction ownership for JOIN mapping too.
        return WithCommandAsync(sql, param, options, nameof(QueryAsync), async (connection, definition) =>
            (IReadOnlyList<TResult>)(await connection.QueryAsync(definition, map, splitOn)).AsList());
    }

    /// <summary>
    /// Splits each buffered row into three objects without adding relationship aggregation or another mapping engine.
    /// </summary>
    /// <typeparam name="TFirst">The first row segment type.</typeparam>
    /// <typeparam name="TSecond">The second row segment type.</typeparam>
    /// <typeparam name="TThird">The third row segment type.</typeparam>
    /// <typeparam name="TResult">The materialized callback result type.</typeparam>
    /// <param name="sql">Service-owned parameterized SQL, never logged.</param>
    /// <param name="map">The required feature callback, which may reuse global Mapster profiles.</param>
    /// <param name="splitOn">The required comma-separated native split columns.</param>
    /// <param name="param">Optional bound parameters, never logged.</param>
    /// <param name="options">Timeout, cancellation and optional caller-owned transaction.</param>
    /// <returns>The fully buffered mapped rows.</returns>
    public Task<IReadOnlyList<TResult>> QueryAsync<TFirst, TSecond, TThird, TResult>(string sql,
        Func<TFirst, TSecond, TThird, TResult> map, string splitOn, object? param = null, DapperCommandOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentException.ThrowIfNullOrWhiteSpace(splitOn);

        // The callback owns feature shaping; the facade only reuses Dapper's buffered row splitting.
        return WithCommandAsync(sql, param, options, nameof(QueryAsync), async (connection, definition) =>
            (IReadOnlyList<TResult>)(await connection.QueryAsync(definition, map, splitOn)).AsList());
    }

    /// <summary>
    /// Returns the first row, throwing when no row exists.
    /// </summary>
    /// <typeparam name="T">The mapped result type.</typeparam>
    /// <param name="sql">Service-owned parameterized SQL, never consumer-supplied SQL.</param>
    /// <param name="param">The optional bound parameter object; never logged.</param>
    /// <param name="options">Timeout, cancellation and optional caller-owned transaction.</param>
    /// <returns>A task containing the mapped result.</returns>
    public Task<T> QueryFirstAsync<T>(string sql, object? param = null, DapperCommandOptions? options = null) =>
        WithCommandAsync(sql, param, options, nameof(QueryFirstAsync), (connection, definition) => connection.QueryFirstAsync<T>(definition));

    /// <summary>
    /// Returns the first row, or default when no row exists.
    /// </summary>
    /// <typeparam name="T">The mapped result type.</typeparam>
    /// <param name="sql">Service-owned parameterized SQL, never consumer-supplied SQL.</param>
    /// <param name="param">The optional bound parameter object; never logged.</param>
    /// <param name="options">Timeout, cancellation and optional caller-owned transaction.</param>
    /// <returns>A task containing the mapped result.</returns>
    public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, DapperCommandOptions? options = null) =>
        WithCommandAsync(sql, param, options, nameof(QueryFirstOrDefaultAsync), (connection, definition) => connection.QueryFirstOrDefaultAsync<T>(definition));

    /// <summary>
    /// Requires exactly one row and rejects zero or multiple rows.
    /// </summary>
    /// <typeparam name="T">The mapped result type.</typeparam>
    /// <param name="sql">Service-owned parameterized SQL, never consumer-supplied SQL.</param>
    /// <param name="param">The optional bound parameter object; never logged.</param>
    /// <param name="options">Timeout, cancellation and optional caller-owned transaction.</param>
    /// <returns>A task containing the mapped result.</returns>
    public Task<T> QuerySingleAsync<T>(string sql, object? param = null, DapperCommandOptions? options = null) =>
        WithCommandAsync(sql, param, options, nameof(QuerySingleAsync), (connection, definition) => connection.QuerySingleAsync<T>(definition));

    /// <summary>
    /// Returns a single row or default, rejecting multiple rows.
    /// </summary>
    /// <typeparam name="T">The mapped result type.</typeparam>
    /// <param name="sql">Service-owned parameterized SQL, never consumer-supplied SQL.</param>
    /// <param name="param">The optional bound parameter object; never logged.</param>
    /// <param name="options">Timeout, cancellation and optional caller-owned transaction.</param>
    /// <returns>A task containing the mapped result.</returns>
    public Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param = null, DapperCommandOptions? options = null) =>
        WithCommandAsync(sql, param, options, nameof(QuerySingleOrDefaultAsync), (connection, definition) => connection.QuerySingleOrDefaultAsync<T>(definition));

    /// <summary>
    /// Executes a command without taking ownership of a caller transaction.
    /// </summary>
    /// <param name="sql">Service-owned parameterized SQL, never consumer-supplied SQL.</param>
    /// <param name="param">The optional bound parameter object; never logged.</param>
    /// <param name="options">Timeout, cancellation and optional caller-owned transaction.</param>
    /// <returns>A task containing the affected row count.</returns>
    public Task<int> ExecuteAsync(string sql, object? param = null, DapperCommandOptions? options = null) =>
        WithCommandAsync(sql, param, options, nameof(ExecuteAsync), (connection, definition) => connection.ExecuteAsync(definition));

    /// <summary>
    /// Returns the first scalar value or default.
    /// </summary>
    /// <typeparam name="T">The mapped result type.</typeparam>
    /// <param name="sql">Service-owned parameterized SQL, never consumer-supplied SQL.</param>
    /// <param name="param">The optional bound parameter object; never logged.</param>
    /// <param name="options">Timeout, cancellation and optional caller-owned transaction.</param>
    /// <returns>A task containing the mapped result.</returns>
    public Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null, DapperCommandOptions? options = null) =>
        WithCommandAsync(sql, param, options, nameof(ExecuteScalarAsync), (connection, definition) => connection.ExecuteScalarAsync<T>(definition));

    /// <summary>
    /// Consumes multiple result sets within a bounded callback lifetime.
    /// </summary>
    /// <typeparam name="TResult">The mapped result type.</typeparam>
    /// <param name="sql">Service-owned parameterized SQL, never consumer-supplied SQL.</param>
    /// <param name="map">Consumes every required result set inside the callback; must not return readers or deferred sequences.</param>
    /// <param name="param">The optional bound parameter object; never logged.</param>
    /// <param name="options">Timeout, cancellation and optional caller-owned transaction.</param>
    /// <returns>A task containing the mapped result.</returns>
    public Task<TResult> QueryMultipleAsync<TResult>(string sql, Func<SqlMapper.GridReader, Task<TResult>> map, object? param = null, DapperCommandOptions? options = null) 
    {
        ArgumentNullException.ThrowIfNull(map);

        return WithCommandAsync(sql, param, options, nameof(QueryMultipleAsync), async (connection, definition) =>
        {
            // The reader cannot outlive its callback, including callback failure and cancellation.
            var grid = await connection.QueryMultipleAsync(definition);
            var operationFailed = false;
            try
            {
                return await map(grid);
            }
            catch
            {
                // Mark the primary failure so cleanup cannot replace it; rethrow preserves its original stack.
                operationFailed = true;
                throw;
            }
            finally
            {
                await DisposeOwnedAsync(grid, operationFailed, nameof(QueryMultipleAsync));
            }
        });
    }

    /// <summary>
    /// Runs one buffered command on an owned connection or a live borrowed transaction.
    /// </summary>
    /// <typeparam name="TResult">The fully materialized result type.</typeparam>
    /// <param name="sql">The service-owned SQL.</param>
    /// <param name="param">The bound parameters, excluded from diagnostics.</param>
    /// <param name="options">The immutable command settings.</param>
    /// <param name="operation">The metadata-only operation name.</param>
    /// <param name="action">The native Dapper operation.</param>
    /// <returns>The completed result before an owned connection is released.</returns>
    private async Task<TResult> WithCommandAsync<TResult>(
        string sql, object? param, DapperCommandOptions? options, string operation,
        Func<IDbConnection, CommandDefinition, Task<TResult>> action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        options ??= new DapperCommandOptions();
        ArgumentOutOfRangeException.ThrowIfNegative(options.CommandTimeout);
        options.CancellationToken.ThrowIfCancellationRequested();

        // Buffer all query results and propagate cancellation into the provider; never log SQL or parameters.
        var definition = new CommandDefinition(sql, param, options.Transaction, options.CommandTimeout,
            options.CommandType, CommandFlags.Buffered, options.CancellationToken);
        _logger.LogDebug(CoreLogConstants.DatabaseLogs.STARTED, operation, options.Transaction is not null);

        TResult result;
        if (options.Transaction is not null)
        {
            var connection = options.Transaction.Connection;
            if (connection is null || connection.State != ConnectionState.Open)
            {
                throw new InvalidOperationException(CoreErrorConstants.DatabaseErrors.TRANSACTION_CONNECTION_REQUIRED);
            }

            // EF/caller owns the transaction and its connection. Do not save, commit, rollback or dispose them.
            result = await action(connection, definition);
        }
        else
        {
            var connection = await _factory.GetOpenConnectionAsync(options.CancellationToken);
            var operationFailed = false;
            try
            {
                result = await action(connection, definition);
            }
            catch
            {
                // Mark the primary failure so cleanup cannot replace it; rethrow preserves its original stack.
                operationFailed = true;
                throw;
            }
            finally
            {
                await DisposeOwnedAsync(connection, operationFailed, operation);
            }
        }

        _logger.LogDebug(CoreLogConstants.DatabaseLogs.COMPLETED, operation);

        return result;
    }

    /// <summary>
    /// Releases an owned connection or reader without replacing a command, callback or cancellation failure.
    /// </summary>
    /// <param name="resource">The resource whose lifetime belongs to this facade.</param>
    /// <param name="operationFailed">Whether an earlier operation failure is already being rethrown.</param>
    /// <param name="operation">The safe command operation name.</param>
    /// <returns>The asynchronous cleanup attempt.</returns>
    private async ValueTask DisposeOwnedAsync(IAsyncDisposable resource, bool operationFailed, string operation)
    {
        try
        {
            await resource.DisposeAsync();
        }
        catch (Exception cleanupFailure) when (operationFailed)
        {
            // Keep the primary failure; without one, this filter does not match and cleanup errors remain observable.
            _logger.LogWarning(CoreLogConstants.DatabaseLogs.CLEANUP_FAILED,
                operation, resource.GetType().Name, cleanupFailure.GetType().Name);
        }
    }
}

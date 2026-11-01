namespace ContextHub.Core.Interfaces.Persistence;

/// <summary>
/// Provides Infrastructure with Dapper operations without replacing business repositories or EF writes.
/// </summary>
public interface IDapperService
{
    /// <summary>
    /// Returns all rows, materialized before an owned connection is released.
    /// </summary>
    /// <typeparam name="T">The mapped result type.</typeparam>
    /// <param name="sql">Service-owned parameterized SQL, never consumer-supplied SQL.</param>
    /// <param name="param">The optional bound parameter object; never logged.</param>
    /// <param name="options">Timeout, cancellation and optional caller-owned transaction.</param>
    /// <returns>A task containing the mapped result.</returns>
    Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? param = null, DapperCommandOptions? options = null);

    /// <summary>
    /// Splits each buffered SQL row into two objects and maps them within the owned connection lifetime.
    /// </summary>
    /// <typeparam name="TFirst">The first row segment type.</typeparam>
    /// <typeparam name="TSecond">The second row segment type; may be nullable for a LEFT JOIN.</typeparam>
    /// <typeparam name="TResult">The materialized callback result type.</typeparam>
    /// <param name="sql">Service-owned parameterized SQL, never logged.</param>
    /// <param name="map">The required feature callback; it may use the configured global Mapster profiles.</param>
    /// <param name="splitOn">The required column where the second row segment starts.</param>
    /// <param name="param">Optional bound parameters, never logged.</param>
    /// <param name="options">Timeout, cancellation and optional caller-owned transaction.</param>
    /// <returns>The buffered mapped rows, available after an owned connection is released.</returns>
    Task<IReadOnlyList<TResult>> QueryAsync<TFirst, TSecond, TResult>(
        string sql,
        Func<TFirst, TSecond, TResult> map,
        string splitOn,
        object? param = null,
        DapperCommandOptions? options = null);

    /// <summary>
    /// Splits each buffered SQL row into three objects without automatically grouping one-to-many relationships.
    /// </summary>
    /// <typeparam name="TFirst">The first row segment type.</typeparam>
    /// <typeparam name="TSecond">The second row segment type.</typeparam>
    /// <typeparam name="TThird">The third row segment type.</typeparam>
    /// <typeparam name="TResult">The materialized callback result type.</typeparam>
    /// <param name="sql">Service-owned parameterized SQL, never logged.</param>
    /// <param name="map">The required feature callback; it may use the configured global Mapster profiles.</param>
    /// <param name="splitOn">The required comma-separated native split columns.</param>
    /// <param name="param">Optional bound parameters, never logged.</param>
    /// <param name="options">Timeout, cancellation and optional caller-owned transaction.</param>
    /// <returns>The buffered mapped rows, available after an owned connection is released.</returns>
    Task<IReadOnlyList<TResult>> QueryAsync<TFirst, TSecond, TThird, TResult>(
        string sql,
        Func<TFirst, TSecond, TThird, TResult> map,
        string splitOn,
        object? param = null,
        DapperCommandOptions? options = null);

    /// <summary>
    /// Returns the first row, throwing when no row exists.
    /// </summary>
    /// <typeparam name="T">The mapped result type.</typeparam>
    /// <param name="sql">Service-owned parameterized SQL, never consumer-supplied SQL.</param>
    /// <param name="param">The optional bound parameter object; never logged.</param>
    /// <param name="options">Timeout, cancellation and optional caller-owned transaction.</param>
    /// <returns>A task containing the mapped result.</returns>
    Task<T> QueryFirstAsync<T>(string sql, object? param = null, DapperCommandOptions? options = null);

    /// <summary>
    /// Returns the first row, or default when no row exists.
    /// </summary>
    /// <typeparam name="T">The mapped result type.</typeparam>
    /// <param name="sql">Service-owned parameterized SQL, never consumer-supplied SQL.</param>
    /// <param name="param">The optional bound parameter object; never logged.</param>
    /// <param name="options">Timeout, cancellation and optional caller-owned transaction.</param>
    /// <returns>A task containing the mapped result.</returns>
    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, DapperCommandOptions? options = null);

    /// <summary>
    /// Requires exactly one row and rejects zero or multiple rows.
    /// </summary>
    /// <typeparam name="T">The mapped result type.</typeparam>
    /// <param name="sql">Service-owned parameterized SQL, never consumer-supplied SQL.</param>
    /// <param name="param">The optional bound parameter object; never logged.</param>
    /// <param name="options">Timeout, cancellation and optional caller-owned transaction.</param>
    /// <returns>A task containing the mapped result.</returns>
    Task<T> QuerySingleAsync<T>(string sql, object? param = null, DapperCommandOptions? options = null);

    /// <summary>
    /// Returns a single row or default, rejecting multiple rows.
    /// </summary>
    /// <typeparam name="T">The mapped result type.</typeparam>
    /// <param name="sql">Service-owned parameterized SQL, never consumer-supplied SQL.</param>
    /// <param name="param">The optional bound parameter object; never logged.</param>
    /// <param name="options">Timeout, cancellation and optional caller-owned transaction.</param>
    /// <returns>A task containing the mapped result.</returns>
    Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param = null, DapperCommandOptions? options = null);

    /// <summary>
    /// Executes a command without taking ownership of a caller transaction.
    /// </summary>
    /// <param name="sql">Service-owned parameterized SQL, never consumer-supplied SQL.</param>
    /// <param name="param">The optional bound parameter object; never logged.</param>
    /// <param name="options">Timeout, cancellation and optional caller-owned transaction.</param>
    /// <returns>A task containing the affected row count.</returns>
    Task<int> ExecuteAsync(string sql, object? param = null, DapperCommandOptions? options = null);

    /// <summary>
    /// Returns the first scalar value or default.
    /// </summary>
    /// <typeparam name="T">The mapped result type.</typeparam>
    /// <param name="sql">Service-owned parameterized SQL, never consumer-supplied SQL.</param>
    /// <param name="param">The optional bound parameter object; never logged.</param>
    /// <param name="options">Timeout, cancellation and optional caller-owned transaction.</param>
    /// <returns>A task containing the mapped result.</returns>
    Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null, DapperCommandOptions? options = null);

    /// <summary>
    /// Consumes multiple result sets within a bounded callback lifetime.
    /// </summary>
    /// <typeparam name="TResult">The mapped result type.</typeparam>
    /// <param name="sql">Service-owned parameterized SQL, never consumer-supplied SQL.</param>
    /// <param name="map">Consumes every required result set inside the callback; must not return readers or deferred sequences.</param>
    /// <param name="param">The optional bound parameter object; never logged.</param>
    /// <param name="options">Timeout, cancellation and optional caller-owned transaction.</param>
    /// <returns>A task containing the mapped result.</returns>
    Task<TResult> QueryMultipleAsync<TResult>(
        string sql,
        Func<SqlMapper.GridReader, Task<TResult>> map,
        object? param = null,
        DapperCommandOptions? options = null);
}

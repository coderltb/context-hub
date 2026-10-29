namespace ContextHub.Shared.Dtos.Options.Persistence;

/// <summary>
/// Carries command settings without provider or connection-string overrides.
/// </summary>
public sealed record DapperCommandOptions
{
    /// <summary>
    /// Defaults to parameterized SQL text.
    /// </summary>
    public CommandType CommandType { get; init; } = CommandType.Text;

    /// <summary>
    /// Bounds execution time in seconds; zero means no timeout only when explicitly requested.
    /// </summary>
    public int CommandTimeout { get; init; } = SharedConstants.DatabaseVariables.COMMAND_TIMEOUT_SECONDS;

    /// <summary>
    /// Borrows an existing transaction; its connection and completion remain caller-owned.
    /// </summary>
    public IDbTransaction? Transaction { get; init; }

    /// <summary>
    /// Propagates cancellation through opening and command execution.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }
}

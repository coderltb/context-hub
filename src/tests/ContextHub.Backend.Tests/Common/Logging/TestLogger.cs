namespace ContextHub.Backend.Tests.Common.Logging;

/// <summary>
/// Captures structured log entries for focused Core behavior tests.
/// </summary>
/// <typeparam name="TCategory">The logger category type.</typeparam>
internal sealed class TestLogger<TCategory> : ILogger<TCategory>
{
    /// <summary>
    /// Stores captured log entries in the order they were written.
    /// </summary>
    private readonly List<LogEntry> _entries = [];

    /// <summary>
    /// Gets the entries written during the test.
    /// </summary>
    public IReadOnlyList<LogEntry> Entries => _entries;

    /// <summary>
    /// Creates a no-op logging scope.
    /// </summary>
    /// <typeparam name="TState">The structured logging state type.</typeparam>
    /// <param name="state">The structured logging state passed through the logger contract.</param>
    /// <returns>A no-op scope used by this test logger.</returns>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => NullScope.Instance;

    /// <summary>
    /// Enables every level so tests can assert the intended severity.
    /// </summary>
    /// <param name="logLevel">The severity evaluated or recorded by the test logger.</param>
    /// <returns>Whether the tested predicate or contract condition holds.</returns>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <summary>
    /// Captures one formatted log entry.
    /// </summary>
    /// <typeparam name="TState">The structured logging state type.</typeparam>
    /// <param name="logLevel">The severity evaluated or recorded by the test logger.</param>
    /// <param name="eventId">The event identifier supplied with the log entry.</param>
    /// <param name="state">The structured logging state passed through the logger contract.</param>
    /// <param name="exception">The optional exception supplied by the calling logger.</param>
    /// <param name="formatter">The delegate formatting this logger's structured state.</param>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        _entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));

    /// <summary>
    /// Represents one captured log entry.
    /// </summary>
    /// <param name="Level">The log severity.</param>
    /// <param name="Message">The formatted structured message.</param>
    /// <param name="Exception">The optional logged exception.</param>
    internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    /// <summary>
    /// Provides a reusable no-op scope for the capturing test logger.
    /// </summary>
    private sealed class NullScope : IDisposable
    {
        /// <summary>
        /// Gets the reusable scope used when tests do not inspect logging scopes.
        /// </summary>
        public static NullScope Instance { get; } = new();

        /// <summary>
        /// Completes the no-op logging scope without owning external resources.
        /// </summary>
        public void Dispose()
        {
        }
    }
}

namespace ContextHub.Shared.Dtos.Options.Caching;

/// <summary>
/// Describes one host-owned Redis endpoint independently of authentication policy or a connection-string parser.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class RedisOptions
{
    /// <summary>
    /// Gets or sets the service endpoint hostname without embedding credentials.
    /// </summary>
    public string Host { get; set; } = "";

    /// <summary>
    /// Gets or sets the Redis endpoint port.
    /// </summary>
    public int Port { get; set; } = 6379;

    /// <summary>
    /// Gets or sets the externally supplied credential, which must never enter diagnostics.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets whether the native client uses TLS.
    /// </summary>
    public bool Ssl { get; set; }

    /// <summary>
    /// Gets or sets the logical database selected by this host.
    /// </summary>
    public int DbNumber { get; set; }

    /// <summary>
    /// Gets or sets the native connect retry count; null preserves the client default.
    /// </summary>
    public int? ConnectRetry { get; set; }

    /// <summary>
    /// Gets or sets the native connect timeout in milliseconds; null preserves the client default.
    /// </summary>
    public int? ConnectTimeout { get; set; }

    /// <summary>
    /// Gets or sets the native synchronous timeout in milliseconds; null preserves the client default.
    /// </summary>
    public int? SyncTimeout { get; set; }

    /// <summary>
    /// Gets or sets the native asynchronous timeout in milliseconds; null preserves the client default.
    /// </summary>
    public int? AsyncTimeout { get; set; }

    /// <summary>
    /// Gets or sets the native connection-failure policy; null preserves the client default.
    /// </summary>
    public bool? AbortOnConnectFail { get; set; }
}

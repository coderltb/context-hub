namespace ContextHub.Authentication.Api.Constants;

/// <summary>
/// Defines synthetic documentation values without reading configuration, requests or persisted credentials.
/// </summary>
public static class AuthenticationExampleConstants
{
    /// <summary>
    /// Identifies a synthetic Owner account.
    /// </summary>
    public static readonly Guid USER_ID = new("00000000-0000-4000-8000-000000000101");

    /// <summary>
    /// Identifies a synthetic client application.
    /// </summary>
    public static readonly Guid CLIENT_ID = new("00000000-0000-4000-8000-000000000201");

    /// <summary>
    /// Identifies synthetic credential metadata.
    /// </summary>
    public static readonly Guid TOKEN_ID = new("00000000-0000-4000-8000-000000000301");

    /// <summary>
    /// Keeps example creation and update timestamps deterministic.
    /// </summary>
    public static readonly DateTimeOffset CREATED_AT = new(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Illustrates an explicit future credential expiry without reading a clock.
    /// </summary>
    public static readonly DateTimeOffset EXPIRES_AT = new(2099, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Represents an opaque version copied from a prior read.
    /// </summary>
    public const string CONCURRENCY_TOKEN = "example-version-1";

    /// <summary>
    /// Names the synthetic Owner account.
    /// </summary>
    public const string USER_NAME = "example.owner";

    /// <summary>
    /// Uses a reserved example domain instead of a real recipient.
    /// </summary>
    public const string EMAIL = "owner@example.test";

    /// <summary>
    /// Illustrates a tool name without claiming a provisioned catalog tool.
    /// </summary>
    public const string TOOL_NAME = "example-tool";

    /// <summary>
    /// Marks the returned secret as synthetic and unusable.
    /// </summary>
    public const string TOKEN = "example-client-token-not-valid";
}

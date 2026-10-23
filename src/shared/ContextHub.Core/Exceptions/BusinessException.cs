namespace ContextHub.Core.Exceptions;

/// <summary>
/// Carries expected business failure context without coupling Application code to HTTP response policy.
/// </summary>
public class BusinessException : Exception
{
    /// <summary>
    /// Validates required feature-owned diagnostic context at its construction boundary.
    /// </summary>
    /// <param name="errorCode">The feature-owned internal error identifier.</param>
    /// <param name="publicMessage">The explicit description; global HTTP handling deliberately does not publish it.</param>
    /// <param name="kind">The neutral failure category interpreted by the selected transport adapter.</param>
    public BusinessException(string errorCode, string publicMessage, BusinessErrorKind kind = BusinessErrorKind.Invalid)
        : base(publicMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicMessage);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), CoreErrorConstants.BusinessErrors.KIND_UNSUPPORTED);
        }

        // Retain feature context for internal handling; adapters own which information is safe on their wire.
        ErrorCode = errorCode;
        PublicMessage = publicMessage;
        Kind = kind;
    }

    /// <summary>
    /// Gets the feature's diagnostic identifier, not an automatically published API error code.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Gets the declared description; its name does not authorize global handlers to expose it.
    /// </summary>
    public string PublicMessage { get; }

    /// <summary>
    /// Gets the transport-neutral classification rather than an HTTP status.
    /// </summary>
    public BusinessErrorKind Kind { get; }
}

namespace ContextHub.Core.Exceptions;

/// <summary>
/// Identifies an unavailable dependency at an adapter boundary without publishing provider messages or status bodies.
/// </summary>
public class DependencyUnavailableException : Exception
{
    /// <summary>
    /// Retains an internal diagnostic identifier and optional provider failure for classification.
    /// </summary>
    /// <param name="errorCode">The adapter-owned diagnostic code, never a response message.</param>
    /// <param name="innerException">The original failure retained for internal diagnosis.</param>
    public DependencyUnavailableException(
        string errorCode,
        Exception? innerException = null)
        : base(errorCode, innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Gets the adapter-owned code used only by diagnostic boundaries.
    /// </summary>
    public string ErrorCode { get; }
}

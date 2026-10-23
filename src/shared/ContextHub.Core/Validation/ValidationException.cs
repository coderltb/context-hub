namespace ContextHub.Core.Validation;

/// <summary>
/// Represents one or more request-validation failures without coupling Core to HTTP or MCP contracts.
/// </summary>
public sealed class ValidationException : Exception
{
    /// <summary>
    /// Creates a validation exception from the collected failures.
    /// </summary>
    /// <param name="errors">The validation failures reported by the registered validators.</param>
    public ValidationException(IEnumerable<ValidationError> errors)
        : base(CoreErrorConstants.ValidationErrors.FAILURE)
    {
        Errors = errors.ToArray();
    }

    /// <summary>
    /// Gets the complete set of validation failures.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; }
}

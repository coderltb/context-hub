namespace ContextHub.Core.Validation;

/// <summary>
/// Describes one transport-neutral validation failure.
/// </summary>
/// <param name="Field">The request field associated with the failure.</param>
/// <param name="Code">The stable application error code.</param>
/// <param name="Message">The human-readable validation message.</param>
public sealed record ValidationError(string Field, string Code, string Message);

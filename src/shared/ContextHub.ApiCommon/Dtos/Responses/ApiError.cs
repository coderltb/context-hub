namespace ContextHub.ApiCommon.Dtos.Responses;

/// <summary>
/// Contains only safe public error information; exception objects must never be attached.
/// </summary>
/// <param name="Code">The stable machine-readable error code.</param>
/// <param name="Message">The approved English message.</param>
/// <param name="Details">Safe field validation messages; otherwise an empty array.</param>
public sealed record ApiError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("details")] IReadOnlyList<ApiErrorDetail> Details);

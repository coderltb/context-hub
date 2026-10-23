namespace ContextHub.ApiCommon.Dtos.Responses;

/// <summary>
/// Describes validated request-field failures without binding exceptions or attempted values.
/// </summary>
/// <param name="Field">The request field path.</param>
/// <param name="Issue">The approved messages for this field.</param>
public sealed record ApiErrorDetail(
    [property: JsonPropertyName("field")] string Field,
    [property: JsonPropertyName("issue")] IReadOnlyList<string> Issue);

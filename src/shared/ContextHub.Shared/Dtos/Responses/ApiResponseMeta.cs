namespace ContextHub.Shared.Dtos.Responses;

/// <summary>
/// Links the HTTP request with its correlation and W3C trace identifiers.
/// </summary>
/// <param name="RequestId">The current HTTP request identifier.</param>
/// <param name="CorrelationId">The middleware-validated correlation UUID.</param>
/// <param name="TraceId">The W3C trace identifier.</param>
/// <param name="SpanId">The current W3C span identifier.</param>
/// <param name="Version">The API contract version supplied by the host.</param>
/// <param name="RequestTimestamp">The UTC request timestamp.</param>
/// <param name="ResponseTimestamp">The UTC response timestamp.</param>
[PublicAPI]
public sealed record ApiResponseMeta(
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("traceId")] string TraceId,
    [property: JsonPropertyName("spanId")] string SpanId,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("requestTimestamp")] DateTimeOffset RequestTimestamp,
    [property: JsonPropertyName("responseTimestamp")] DateTimeOffset ResponseTimestamp);

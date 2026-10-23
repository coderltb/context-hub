namespace ContextHub.ApiCommon.Dtos.Responses;

/// <summary>
/// Marks intentionally created API envelopes; filters must not wrap unrelated payloads.
/// </summary>
public interface IApiResponse
{
    /// <summary>
    /// Gets or sets request metadata immediately before response serialization.
    /// </summary>
    ApiResponseMeta? Meta { get; set; }
}

namespace ContextHub.Backend.Tests.Shared.ContextHub.ApiCommon.Extensions;

/// <summary>
/// Verifies approved API status mappings and rejects unsupported statuses without producing a fallback error.
/// </summary>
public sealed class HttpStatusCodeExtensionsTests
{
    /// <summary>
    /// Verifies that every supported status resolves to the stable public error code and a non-empty message.
    /// </summary>
    /// <param name="status">The HTTP status selected for the scenario.</param>
    /// <param name="expectedCode">The error code required by the asserted contract.</param>
    [Theory]
    [InlineData(400, "error_validation")]
    [InlineData(401, "error_unauthorized")]
    [InlineData(403, "error_forbidden")]
    [InlineData(404, "error_not_found")]
    [InlineData(405, "error_method_not_allowed")]
    [InlineData(406, "error_not_acceptable")]
    [InlineData(409, "error_conflict")]
    [InlineData(413, "error_payload_too_large")]
    [InlineData(415, "error_unsupported_media_type")]
    [InlineData(429, "error_rate_limited")]
    [InlineData(500, "error_internal")]
    [InlineData(503, "error_dependency_unavailable")]
    public void ApprovedStatus_MapsToSafeError(int status, string expectedCode)
    {
        Assert.True(status.IsSupportedApiError());
        var (code, message) = status.ToApiError();

        Assert.Equal(expectedCode, code);
        Assert.False(string.IsNullOrWhiteSpace(message));

        // OpenAPI fixtures must publish the same string contract, not numeric enum values or legacy aliases.
        var example = ApiErrorExampleFactory.Create(status, "2.0");
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(example));
        var error = document.RootElement.GetProperty("error");

        Assert.Equal(expectedCode, error.GetProperty("code").GetString());
        Assert.Equal(message, error.GetProperty("message").GetString());
    }

    /// <summary>
    /// Verifies that non-approved statuses are neither advertised nor silently mapped to a different error.
    /// </summary>
    /// <param name="status">The HTTP status selected for the scenario.</param>
    [Theory]
    [InlineData(200)]
    [InlineData(302)]
    [InlineData(418)]
    public void UnsupportedStatus_IsRejected(int status)
    {
        Assert.False(status.IsSupportedApiError());
        Assert.Throws<ArgumentOutOfRangeException>(() => status.ToApiError());
    }
}

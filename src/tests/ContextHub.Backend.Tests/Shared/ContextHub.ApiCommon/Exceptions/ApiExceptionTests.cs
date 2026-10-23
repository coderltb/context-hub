namespace ContextHub.Backend.Tests.Shared.ContextHub.ApiCommon.Exceptions;

/// <summary>
/// Verifies supported transport status defaults without introducing a second status mapper.
/// </summary>
public sealed class ApiExceptionTests
{
    /// <summary>
    /// Supplies the established generic error when optional diagnostic text is absent.
    /// </summary>
    /// <param name="status">The approved HTTP status.</param>
    /// <param name="code">The independently specified standard error code.</param>
    /// <param name="message">The independently specified standard message.</param>
    [Theory]
    [InlineData(400, "error_validation", "Validation failures have occurred.")]
    [InlineData(404, "error_not_found", "The requested resource was not found.")]
    [InlineData(500, "error_internal", "An unexpected error occurred.")]
    public void Constructor_UsesStandardDefaults(int status, string code, string message)
    {
        var failure = new ApiException(status);

        Assert.Equal(status, failure.StatusCode);
        Assert.Equal(code, failure.ErrorCode);
        Assert.Equal(message, failure.PublicMessage);
    }

    /// <summary>
    /// Treats blank optional diagnostic overrides as absent instead of rejecting optional input.
    /// </summary>
    [Fact]
    public void Constructor_BlankOptionalValuesUseStandardDefaults()
    {
        var failure = new ApiException(409, " ", "");

        Assert.Equal("error_conflict", failure.ErrorCode);
        Assert.Equal("The operation conflicts with the current resource state.", failure.PublicMessage);
    }

    /// <summary>
    /// Rejects statuses without a supported error contract at construction rather than during response writing.
    /// </summary>
    /// <param name="status">The unsupported success, redirect or unmapped failure status.</param>
    [Theory]
    [InlineData(200)]
    [InlineData(302)]
    [InlineData(418)]
    public void Constructor_UnsupportedStatusIsRejected(int status)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ApiException(status));
    }
}

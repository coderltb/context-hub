namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Exceptions;

/// <summary>
/// Verifies neutral business failure inputs without coupling Core to HTTP status codes.
/// </summary>
public sealed class BusinessExceptionTests
{
    /// <summary>
    /// Retains the feature's diagnostic information and defaults an unspecified category to invalid input.
    /// </summary>
    [Fact]
    public void Constructor_DefaultsToInvalidAndRetainsDiagnosticContext()
    {
        var failure = new BusinessException("fixture_code", "Fixture diagnostic message.");

        Assert.Equal(BusinessErrorKind.Invalid, failure.Kind);
        Assert.Equal("fixture_code", failure.ErrorCode);
        Assert.Equal("Fixture diagnostic message.", failure.PublicMessage);
        Assert.Equal(failure.PublicMessage, failure.Message);
    }

    /// <summary>
    /// Rejects absent mandatory diagnostic identifiers at their owning constructor boundary.
    /// </summary>
    /// <param name="code">The possibly missing feature error code.</param>
    /// <param name="message">The possibly missing diagnostic description.</param>
    /// <param name="parameter">The required input expected to fail.</param>
    [Theory]
    [InlineData(null, "description", "errorCode")]
    [InlineData(" ", "description", "errorCode")]
    [InlineData("code", null, "publicMessage")]
    [InlineData("code", " ", "publicMessage")]
    public void Constructor_MissingRequiredInputIsRejected(string? code, string? message, string parameter)
    {
        var error = Assert.ThrowsAny<ArgumentException>(() => new BusinessException(code!, message!));

        Assert.Equal(parameter, error.ParamName);
    }

    /// <summary>
    /// Prevents an undeclared category from reaching a transport adapter with ambiguous semantics.
    /// </summary>
    [Fact]
    public void Constructor_UnknownKindIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new BusinessException("fixture_code", "Fixture diagnostic message.", (BusinessErrorKind)99));
    }
}

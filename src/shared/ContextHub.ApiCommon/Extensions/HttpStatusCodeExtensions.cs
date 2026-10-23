namespace ContextHub.ApiCommon.Extensions;

/// <summary>
/// Maps approved API HTTP status codes to safe public errors without inspecting exception text.
/// </summary>
public static class HttpStatusCodeExtensions
{
    /// <summary>
    /// Checks whether an HTTP status has an approved error contract.
    /// </summary>
    /// <param name="status">The HTTP status.</param>
    /// <returns>True when a stable mapping exists.</returns>
    public static bool IsSupportedApiError(this int status) =>
        status is 400 or 401 or 403 or 404 or 405 or 406 or 409 or 413 or 415 or 429 or 500 or 503;

    /// <summary>
    /// Looks up a safe public error, never deriving it from exception text.
    /// </summary>
    /// <param name="status">The approved HTTP error status.</param>
    /// <returns>The stable error code and English message.</returns>
    public static (string Code, string Message) ToApiError(this int status) => status switch
    {
        400 => (
            ApiCommonErrorConstants.ApiErrors.VALIDATION_ERROR,
            ApiCommonErrorConstants.ApiErrors.VALIDATION_ERROR_MESSAGE),
        401 => (
            ApiCommonErrorConstants.ApiErrors.UNAUTHORIZED,
            ApiCommonErrorConstants.ApiErrors.UNAUTHORIZED_MESSAGE),
        403 => (
            ApiCommonErrorConstants.ApiErrors.FORBIDDEN,
            ApiCommonErrorConstants.ApiErrors.FORBIDDEN_MESSAGE),
        404 => (
            ApiCommonErrorConstants.ApiErrors.NOT_FOUND,
            ApiCommonErrorConstants.ApiErrors.NOT_FOUND_MESSAGE),
        405 => (
            ApiCommonErrorConstants.ApiErrors.METHOD_NOT_ALLOWED,
            ApiCommonErrorConstants.ApiErrors.METHOD_NOT_ALLOWED_MESSAGE),
        406 => (
            ApiCommonErrorConstants.ApiErrors.NOT_ACCEPTABLE,
            ApiCommonErrorConstants.ApiErrors.NOT_ACCEPTABLE_MESSAGE),
        409 => (
            ApiCommonErrorConstants.ApiErrors.CONFLICT,
            ApiCommonErrorConstants.ApiErrors.CONFLICT_MESSAGE),
        413 => (
            ApiCommonErrorConstants.ApiErrors.PAYLOAD_TOO_LARGE,
            ApiCommonErrorConstants.ApiErrors.PAYLOAD_TOO_LARGE_MESSAGE),
        415 => (
            ApiCommonErrorConstants.ApiErrors.UNSUPPORTED_MEDIA_TYPE,
            ApiCommonErrorConstants.ApiErrors.UNSUPPORTED_MEDIA_TYPE_MESSAGE),
        429 => (
            ApiCommonErrorConstants.ApiErrors.RATE_LIMITED,
            ApiCommonErrorConstants.ApiErrors.RATE_LIMITED_MESSAGE),
        500 => (
            ApiCommonErrorConstants.ApiErrors.INTERNAL_ERROR,
            ApiCommonErrorConstants.ApiErrors.INTERNAL_ERROR_MESSAGE),
        503 => (
            ApiCommonErrorConstants.ApiErrors.DEPENDENCY_UNAVAILABLE,
            ApiCommonErrorConstants.ApiErrors.DEPENDENCY_UNAVAILABLE_MESSAGE),
        _ => throw new ArgumentOutOfRangeException(nameof(status), ApiCommonErrorConstants.ApiErrors.STATUS_UNSUPPORTED)
    };
}

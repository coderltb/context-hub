namespace ContextHub.ApiCommon.Handlers.Exceptions;

/// <summary>
/// Maps exceptions only for API JSON routes, keeping native protocols and cancellations untouched.
/// </summary>
/// <param name="boundary">The host-owned API route scope.</param>
/// <param name="factory">The safe API error factory.</param>
/// <param name="logger">The structured logger; exception messages and payloads are excluded.</param>
public sealed class ApiExceptionHandler(
    ApiResponseBoundary boundary,
    ApiErrorResponseFactory factory,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>
    /// Classifies failures while publishing only the standard status contract, never exception-owned details.
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="exception">The error being classified.</param>
    /// <param name="cancellationToken">Stops a response write.</param>
    /// <returns>True only when the API adapter completed the response.</returns>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Do not rewrite native protocols, an already-started response or a disconnected request's cancellation.
        if (!boundary.Contains(httpContext.Request.Path) ||
            httpContext.Response.HasStarted ||
            exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        // Classification selects only a status. Native programmer errors and upstream HTTP statuses are not business contracts.
        var status = exception switch
        {
            CoreAuthenticationException => StatusCodes.Status401Unauthorized,
            CoreAuthorizationException => StatusCodes.Status403Forbidden,
            CoreValidationException => StatusCodes.Status400BadRequest,
            BusinessException { Kind: BusinessErrorKind.NotFound } => StatusCodes.Status404NotFound,
            BusinessException { Kind: BusinessErrorKind.Conflict } => StatusCodes.Status409Conflict,
            BusinessException => StatusCodes.Status400BadRequest,
            ApiException api => api.StatusCode,
            BadHttpRequestException request when request.StatusCode.IsSupportedApiError() => request.StatusCode,
            DistributedLockUnavailableException => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };

        if (exception is CoreValidationException validation)
        {
            // Field names and messages may describe submitted data; global handling retains only a diagnostic count.
            logger.LogWarning(ApiCommonLogConstants.ExceptionLogs.VALIDATION_REJECTED,
                httpContext.TraceIdentifier, validation.Errors.Select(error => error.Field).Distinct().Count());
        }
        else if (exception is BusinessException or ApiException or BadHttpRequestException)
        {
            var diagnosticCode = exception switch
            {
                BusinessException business => business.ErrorCode,
                ApiException api => api.ErrorCode,
                _ => null
            };

            // Keep declared identifiers internal. Never render Message, PublicMessage, InnerException or exception Data.
            if (status >= StatusCodes.Status500InternalServerError)
            {
                logger.LogError(ApiCommonLogConstants.ExceptionLogs.EXPECTED_FAILURE,
                    exception.GetType().Name, status, diagnosticCode, httpContext.TraceIdentifier);
            }
            else
            {
                logger.LogWarning(ApiCommonLogConstants.ExceptionLogs.EXPECTED_FAILURE,
                    exception.GetType().Name, status, diagnosticCode, httpContext.TraceIdentifier);
            }
        }
        else if (exception is not (CoreAuthenticationException or CoreAuthorizationException or DistributedLockUnavailableException))
        {
            // Authentication, policy and lock boundaries own their diagnostics; unexpected failures are recorded once here.
            logger.LogError(ApiCommonLogConstants.ExceptionLogs.UNEXPECTED_FAILURE,
                exception.GetType().Name, httpContext.TraceIdentifier);
        }

        // One standard factory owns every global error body. Exception descriptions never override this security boundary.
        await factory.WriteAsync(httpContext, status, cancellationToken);

        return true;
    }
}

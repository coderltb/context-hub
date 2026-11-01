namespace ContextHub.Core.Behaviors;

/// <summary>
/// Runs every registered FluentValidation validator before a Mediator handler executes.
/// </summary>
/// <typeparam name="TRequest">The Mediator request type.</typeparam>
/// <typeparam name="TResponse">The Mediator response type.</typeparam>
/// <param name="validators">The validators registered for the request.</param>
/// <param name="logger">The structured application logger.</param>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Validates the request and invokes the next handler only when all validators succeed.
    /// </summary>
    /// <param name="message">The request being handled.</param>
    /// <param name="next">The next delegate in the Mediator pipeline.</param>
    /// <param name="cancellationToken">Stops validation and request handling.</param>
    /// <returns>The response returned by the next handler.</returns>
    public async ValueTask<TResponse> Handle(
        TRequest message,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        // Cancellation applies to requests with no validators as well; do not enter a cancelled handler.
        cancellationToken.ThrowIfCancellationRequested();

        // Materialize once so validator enumeration and the logged count describe the same registration set.
        var validatorList = validators as IReadOnlyCollection<IValidator<TRequest>> ?? validators.ToArray();
        logger.LogDebug(CoreLogConstants.ValidationLogs.STARTED, typeof(TRequest).Name, validatorList.Count);
        if (validatorList.Count == 0)
        {
            return await next(message, cancellationToken);
        }

        // Run scoped validators sequentially with fresh validation contexts to isolate mutable state.
        var results = new List<ValidationResult>(validatorList.Count);
        foreach (var validator in validatorList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await validator.ValidateAsync(new ValidationContext<TRequest>(message), cancellationToken));
        }

        var errors = results
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .Select(failure => new ValidationError(failure.PropertyName, failure.ErrorCode, failure.ErrorMessage))
            .Distinct()
            .ToArray();

        // Keep all distinct field/code/message failures for adapters, but log only codes and counts.
        if (errors.Length > 0)
        {
            logger.LogWarning(
                CoreLogConstants.ValidationLogs.REQUEST_REJECTED,
                typeof(TRequest).Name,
                errors.Length,
                string.Join(',', errors.Select(error => error.Code).Distinct(StringComparer.Ordinal)));

            // The API or MCP adapter owns the public error shape; Core only reports neutral failures.
            throw new CoreValidationException(errors);
        }

        logger.LogDebug(CoreLogConstants.ValidationLogs.PASSED, typeof(TRequest).Name);

        return await next(message, cancellationToken);
    }
}

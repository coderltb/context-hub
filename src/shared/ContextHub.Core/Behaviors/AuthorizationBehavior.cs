namespace ContextHub.Core.Behaviors;

/// <summary>
/// Applies service-owned permission policies without inspecting HTTP context, claims or credentials.
/// </summary>
/// <typeparam name="TRequest">The Mediator request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="policies">The owning service's policies for this exact request.</param>
/// <param name="logger">The safe operational logger.</param>
public sealed class AuthorizationBehavior<TRequest, TResponse>(
    IEnumerable<IRequestAuthorizationPolicy<TRequest>> policies,
    ILogger<AuthorizationBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Requires every registered policy to pass and fails closed when a protected request has no policy.
    /// </summary>
    /// <param name="message">The request being authorized.</param>
    /// <param name="next">The next pipeline delegate.</param>
    /// <param name="cancellationToken">Cancels policies and downstream work.</param>
    /// <returns>The authorized handler response.</returns>
    public async ValueTask<TResponse> Handle(TRequest message, MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Snapshot the service policies once; authorization decisions never inspect raw transport credentials here.
        var registered = policies.ToArray();
        if (registered.Length == 0 && message is IRequiresAuthorization)
        {
            // A missing policy registration must never silently downgrade a protected request to public access.
            logger.LogWarning(CoreLogConstants.AuthorizationLogs.POLICY_MISSING, typeof(TRequest).Name);
            throw new CoreAuthorizationException();
        }

        // Policies can share the service DbContext. Sequential evaluation avoids concurrent EF operations.
        foreach (var policy in registered)
        {
            if (!await policy.IsAllowedAsync(message, cancellationToken))
            {
                logger.LogWarning(CoreLogConstants.AuthorizationLogs.DENIED, typeof(TRequest).Name);
                throw new CoreAuthorizationException();
            }
        }

        logger.LogDebug(CoreLogConstants.AuthorizationLogs.PASSED, typeof(TRequest).Name, registered.Length);

        return await next(message, cancellationToken);
    }
}

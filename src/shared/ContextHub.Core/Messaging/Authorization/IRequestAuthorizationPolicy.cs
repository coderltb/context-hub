namespace ContextHub.Core.Messaging.Authorization;

/// <summary>
/// Supplies a service-owned permission decision without requiring transport or token dependencies in Core.
/// </summary>
/// <typeparam name="TRequest">The protected business request.</typeparam>
public interface IRequestAuthorizationPolicy<in TRequest>
{
    /// <summary>
    /// Evaluates current authoritative actor and resource state in the owning service.
    /// </summary>
    /// <param name="request">The business request.</param>
    /// <param name="cancellationToken">Stops the policy evaluation.</param>
    /// <returns>True only when the policy permits the operation.</returns>
    ValueTask<bool> IsAllowedAsync(TRequest request, CancellationToken cancellationToken);
}

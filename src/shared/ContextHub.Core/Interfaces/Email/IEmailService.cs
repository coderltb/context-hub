namespace ContextHub.Core.Interfaces.Email;

/// <summary>
/// Sends approved transactional email through a shared native provider adapter.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Requests provider acceptance without treating it as confirmation of inbox delivery.
    /// </summary>
    /// <param name="request">The message, which must never be serialized into diagnostics.</param>
    /// <param name="cancellationToken">Cancels the caller's send operation.</param>
    /// <returns>Whether the provider accepted the message.</returns>
    Task<bool> SendAsync(EmailRequestDto request, CancellationToken cancellationToken);
}

namespace ContextHub.Core.Services.Email;

/// <summary>
/// Sends one transactional message through the native SDK without logging its recipient, content or provider response body.
/// </summary>
/// <param name="sendGridClient">The DI-owned SDK client using native no-retry defaults.</param>
/// <param name="sendGridOptions">The externally supplied sender configuration.</param>
/// <param name="logger">The provider outcome logger, never a payload logger.</param>
public sealed class SendGridEmailService(
    ISendGridClient sendGridClient,
    IOptions<SendGridOptions> sendGridOptions,
    ILogger<SendGridEmailService> logger) : IEmailService
{
    /// <summary>
    /// Distinguishes provider acceptance from rejection while keeping caller cancellation unchanged.
    /// </summary>
    /// <param name="request">The message, which may contain transient proof and must not be logged.</param>
    /// <param name="cancellationToken">Cancels the SDK send.</param>
    /// <returns>True only when the provider accepted the message; this does not confirm delivery.</returns>
    public async Task<bool> SendAsync(EmailRequestDto request, CancellationToken cancellationToken)
    {
        var message = MailHelper.CreateSingleEmail(
            new EmailAddress(sendGridOptions.Value.FromEmail, sendGridOptions.Value.FromName),
            new EmailAddress(request.Recipient),
            request.Subject,
            request.PlainText,
            request.Html);

        Response response;
        try
        {
            response = await sendGridClient.SendEmailAsync(message, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // SDK timeout is an unsuccessful delivery attempt; explicit caller cancellation must still propagate.
            logger.LogWarning(CoreLogConstants.EmailLogs.TRANSPORT_FAILED, nameof(TimeoutException));

            return false;
        }
        catch (Exception exception) when (exception is HttpRequestException or TimeoutRejectedException or BrokenCircuitException)
        {
            // Expected transport failures cannot confirm acceptance; keep details private and do not retry here.
            logger.LogWarning(CoreLogConstants.EmailLogs.TRANSPORT_FAILED, exception.GetType().Name);

            return false;
        }

        // Cleanup belongs to the completed response, not the SDK send-failure fallback.
        using var body = response.Body;
        var accepted = (int)response.StatusCode is >= 200 and < 300;
        if (accepted)
        {
            logger.LogInformation(CoreLogConstants.EmailLogs.ACCEPTED, (int)response.StatusCode);
        }
        else
        {
            logger.LogWarning(CoreLogConstants.EmailLogs.REJECTED, (int)response.StatusCode);
        }

        return accepted;
    }
}

namespace ContextHub.Shared.Dtos.Requests.Emails;

/// <summary>
/// Describes one transactional email without exposing provider configuration or a generic public sending API.
/// </summary>
/// <param name="recipient">The intended destination; never log this value.</param>
/// <param name="subject">The service-owned subject.</param>
/// <param name="plainText">The plain-text message, potentially containing a transient secret.</param>
/// <param name="html">The independently authored HTML message.</param>
public sealed class EmailRequestDto(
    string recipient,
    string subject,
    string plainText,
    string html)
{
    /// <summary>
    /// Gets the intended destination.
    /// </summary>
    public string Recipient { get; } = recipient;

    /// <summary>
    /// Gets the service-owned subject.
    /// </summary>
    public string Subject { get; } = subject;

    /// <summary>
    /// Gets the plain-text content, which must never enter diagnostics.
    /// </summary>
    public string PlainText { get; } = plainText;

    /// <summary>
    /// Gets the HTML content, which must never enter diagnostics.
    /// </summary>
    public string Html { get; } = html;
}

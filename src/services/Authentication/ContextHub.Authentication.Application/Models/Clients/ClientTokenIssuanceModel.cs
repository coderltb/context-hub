namespace ContextHub.Authentication.Application.Models.Clients;

/// <summary>
/// Carries generated credential material only until mapping separates persistence data from the one-time response.
/// </summary>
/// <param name="ClientId">The receiving application.</param>
/// <param name="TokenId">The generated public identifier.</param>
/// <param name="Prefix">The non-secret display prefix.</param>
/// <param name="Token">The transient bearer value, never persisted or logged.</param>
/// <param name="SecretHash">The digest mapped only to the internal persistence model.</param>
/// <param name="AllowedTools">The snapshotted exact tool grants.</param>
/// <param name="ExpiresAt">The optional UTC expiry; null means no fixed expiration.</param>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ClientTokenIssuanceModel(
    Guid ClientId,
    Guid TokenId,
    string Prefix,
    string Token,
    byte[] SecretHash,
    IReadOnlyList<string> AllowedTools,
    DateTimeOffset? ExpiresAt = null);

namespace ContextHub.Authentication.Application.Models.Sessions;

/// <summary>
/// Carries protocol metadata from the native adapter; caller identity is read through the authenticated accessor.
/// </summary>
/// <param name="ClientId">The native-validated protocol client identifier.</param>
/// <param name="Scopes">The scopes extracted from the protocol request.</param>
/// <param name="ProtocolRequirementsSatisfied">Whether scope, state, nonce and PKCE requirements were met.</param>
public sealed record AuthorizationGrantRequestModel(
    string? ClientId,
    IReadOnlyList<string> Scopes,
    bool ProtocolRequirementsSatisfied);

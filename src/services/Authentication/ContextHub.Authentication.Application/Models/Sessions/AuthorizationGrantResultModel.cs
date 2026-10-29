namespace ContextHub.Authentication.Application.Models.Sessions;

/// <summary>
/// Returns an explicit decision and only the approved BCL principal, leaving HTTP results in the protocol adapter.
/// </summary>
/// <param name="Decision">The use case's fail-closed grant decision.</param>
/// <param name="Principal">The allowlisted principal, present only for an accepted grant.</param>
public sealed record AuthorizationGrantResultModel(
    AuthorizationGrantDecision Decision,
    ClaimsPrincipal? Principal = null);

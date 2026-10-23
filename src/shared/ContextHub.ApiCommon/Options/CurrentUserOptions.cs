namespace ContextHub.ApiCommon.Options;

/// <summary>
/// Configures already-normalized claim names without choosing a token validation strategy.
/// </summary>
public sealed class CurrentUserOptions
{
    /// <summary>
    /// Identifies the subject claim; hosts with native mapping can select NameIdentifier explicitly.
    /// </summary>
    public string SubjectClaimType { get; set; } = CoreConstants.AuthenticationVariables.SUBJECT_CLAIM;
}

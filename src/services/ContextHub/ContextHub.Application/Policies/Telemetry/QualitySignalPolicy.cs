namespace ContextHub.Application.Policies.Telemetry;

/// <summary>
/// Defines the closed relationship between issue codes and package references.
/// </summary>
public static class QualitySignalPolicy
{
    /// <summary>
    /// Rejects free text and cross-category issue codes before any lookup or write.
    /// </summary>
    /// <param name="request">The coded public signal.</param>
    /// <returns>Whether the package fields and fixed issue codes are consistent.</returns>
    public static bool IsValidCombination(QualitySignalRequestDto request)
    {
        var hasPackage = request.PackageId is { Length: > 0 and <= 160 } && request.Version is { Length: > 0 and <= 64 };
        return request.SignalType switch
        {
            "coverage_gap" => (request.IssueCode is "no_matching_package" or "existing_packages_inapplicable") &&
                request.PackageId is null && request.Version is null && request.WorkflowStepId is null &&
                (request.DesiredPackageType is null or "agent" or "skill" or "rule" or "workflow"),
            "package_issue" => hasPackage && request.DesiredPackageType is null &&
                (request.IssueCode is "instruction_incorrect" or "instruction_ambiguous" or "missing_step" or "invalid_dependency" or "unsafe_instruction"),
            "client_incompatible" => hasPackage && request.DesiredPackageType is null &&
                (request.IssueCode is "capability_unavailable" or "structured_content_unavailable" or "tool_call_unsupported"),
            _ => false
        };
    }
}

namespace ContextHub.Infrastructure.Interfaces.Telemetry;

/// <summary>
/// Defines the feature-specific persistence boundary owned by Infrastructure.
/// </summary>
public interface IQualitySignalRepository
{
    /// <summary>
    /// Executes the named authoritative persistence operation.
    /// </summary>
    /// <param name="request">The approved request input.</param>
    /// <param name="cancellationToken">The approved cancellationToken input.</param>
    /// <returns>The completed persistence operation or approved projection.</returns>
    Task<Guid?> ResolvePackageAsync(QualitySignalRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Executes the named authoritative persistence operation.
    /// </summary>
    /// <param name="request">The approved request input.</param>
    /// <param name="clientId">The approved clientId input.</param>
    /// <param name="packageId">The approved packageId input.</param>
    /// <param name="cancellationToken">The approved cancellationToken input.</param>
    /// <returns>The completed persistence operation or approved projection.</returns>
    Task<bool> UpsertAsync(
        QualitySignalRequestDto request,
        Guid clientId,
        Guid? packageId,
        CancellationToken cancellationToken);
}

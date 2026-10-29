namespace ContextHub.Authentication.Application.Dtos.Clients;

/// <summary>
/// Defines a complete replacement of editable client settings.
/// </summary>
/// <param name="DisplayName">The application label.</param>
/// <param name="IsActive">Whether credentials remain usable.</param>
/// <param name="ConcurrencyToken">The concurrencyToken from the latest client response; stale values produce a conflict.</param>
public sealed record UpdateClientRequestDto(
    string DisplayName,
    bool IsActive,
    string ConcurrencyToken);

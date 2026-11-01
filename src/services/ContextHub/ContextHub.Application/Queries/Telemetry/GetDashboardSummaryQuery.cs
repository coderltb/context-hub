namespace ContextHub.Application.Queries.Telemetry;

/// <summary>
/// Dispatches the protected GetSummaryAsync use case.
/// </summary>
/// <param name="Request">The approved request input.</param>
public sealed record GetDashboardSummaryQuery(DashboardRequestDto Request) : IRequest<ApiResponse<DashboardSummaryResponseDto>>, IAdminRequest;

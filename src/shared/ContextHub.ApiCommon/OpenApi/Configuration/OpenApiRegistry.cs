namespace ContextHub.ApiCommon.OpenApi.Configuration;

/// <summary>
/// Holds host-approved mappings without retaining providers, request scopes or runtime payloads.
/// </summary>
/// <param name="Requests">Request DTO to provider mappings.</param>
/// <param name="Values">Bound query/form DTO to provider mappings.</param>
/// <param name="Responses">Wire-type, status and name to provider mappings.</param>
/// <param name="Operations">Request/response contract mappings.</param>
/// <param name="ApiErrors">Whether the host declares its shared API error boundary.</param>
internal sealed record OpenApiRegistry(
    IReadOnlyDictionary<Type, Type> Requests,
    IReadOnlyDictionary<Type, Type> Values,
    IReadOnlyDictionary<(Type Type, int Status, string Name), Type> Responses,
    IReadOnlyDictionary<(Type Request, Type Response), OpenApiOperationContract> Operations,
    bool ApiErrors);

/// <summary>
/// Describes operation statuses independently from example serialization.
/// </summary>
/// <param name="SuccessStatus">The host-declared successful status.</param>
/// <param name="ErrorStatuses">The operation's business-error statuses.</param>
internal sealed record OpenApiOperationContract(int SuccessStatus, IReadOnlyList<int> ErrorStatuses);

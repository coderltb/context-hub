namespace ContextHub.ApiCommon.Attributes.OpenApi;

/// <summary>
/// Declares endpoint-specific success and business errors, optionally overriding a shared DTO-pair contract.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class OpenApiOperationAttribute : Attribute
{
    /// <summary>
    /// Declares a complete endpoint-specific success and business-error contract.
    /// </summary>
    /// <param name="successStatus">The actual success status.</param>
    /// <param name="errorStatuses">The endpoint-specific business failures; empty replaces shared failures.</param>
    public OpenApiOperationAttribute(int successStatus, params int[] errorStatuses)
    {
        ArgumentNullException.ThrowIfNull(errorStatuses);

        // An override is a complete business-status contract, not an additive list inherited from another operation.
        if (successStatus is < 200 or > 299 || errorStatuses.Distinct().Count() != errorStatuses.Length ||
            errorStatuses.Any(status => !status.IsSupportedApiError()))
        {
            throw new InvalidOperationException(ApiCommonErrorConstants.OpenApiErrors.INVALID_CONTRACT);
        }

        SuccessStatus = successStatus;

        // Attribute metadata is shared across document generations, so callers must not mutate its status collection.
        ErrorStatuses = Array.AsReadOnly((int[])errorStatuses.Clone());
    }

    /// <summary>
    /// Gets the endpoint's actual successful status.
    /// </summary>
    public int SuccessStatus { get; }

    /// <summary>
    /// Gets the closed set of endpoint-specific business failures.
    /// </summary>
    public IReadOnlyList<int> ErrorStatuses { get; }
}

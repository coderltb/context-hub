namespace ContextHub.ApiCommon.OpenApi.Configuration;

/// <summary>
/// Builds explicit type mappings once at the host composition root; no provider instances are retained.
/// </summary>
/// <param name="services">The host-owned service collection.</param>
public sealed class OpenApiRegistration(IServiceCollection services)
{
    /// <summary>
    /// Maps each body DTO to its single default transient example provider.
    /// </summary>
    private readonly Dictionary<Type, Type> requests = [];

    /// <summary>
    /// Maps bound query/form DTOs to transient value fixtures independently of JSON body examples.
    /// </summary>
    private readonly Dictionary<Type, Type> values = [];

    /// <summary>
    /// Keeps named response variants separate by exact wire type and status.
    /// </summary>
    private readonly Dictionary<(Type Type, int Status, string Name), Type> responses = [];

    /// <summary>
    /// Maps each request/response pair to its declared success and business-error statuses.
    /// </summary>
    private readonly Dictionary<(Type Request, Type Response), OpenApiOperationContract> operations = [];

    /// <summary>
    /// Records documentation opt-in without installing any runtime response middleware.
    /// </summary>
    private bool apiErrors;

    /// <summary>
    /// Registers a typed request fixture once for all operations accepting this body type.
    /// </summary>
    /// <typeparam name="TRequest">The body DTO.</typeparam>
    /// <typeparam name="TProvider">The transient typed fixture provider.</typeparam>
    /// <returns>This registration for further mappings.</returns>
    public OpenApiRegistration Request<TRequest, TProvider>()
        where TRequest : class where TProvider : class, IOpenApiExampleProvider<TRequest>
    {
        // One default fixture per DTO keeps reused actions consistent; exceptional actions use an attribute override.
        if (!requests.TryAdd(typeof(TRequest), typeof(TProvider)))
        {
            throw new InvalidOperationException(ApiCommonErrorConstants.OpenApiErrors.DUPLICATE_MAPPING);
        }

        RegisterProvider<TProvider>();

        return this;
    }

    /// <summary>
    /// Registers one typed fixture for the native query/form fields of a reusable DTO.
    /// </summary>
    /// <typeparam name="TRequest">The query or form DTO whose fields are actually bound.</typeparam>
    /// <typeparam name="TProvider">The transient synthetic fixture provider.</typeparam>
    /// <returns>This registration for further mappings.</returns>
    public OpenApiRegistration Values<TRequest, TProvider>()
        where TRequest : class where TProvider : class, IOpenApiExampleProvider<TRequest>
    {
        if (!values.TryAdd(typeof(TRequest), typeof(TProvider)))
        {
            throw new InvalidOperationException(ApiCommonErrorConstants.OpenApiErrors.DUPLICATE_MAPPING);
        }

        RegisterProvider<TProvider>();

        return this;
    }

    /// <summary>
    /// Registers an independent named fixture for the exact response type on the wire.
    /// </summary>
    /// <typeparam name="TResponse">The response payload, including any envelope.</typeparam>
    /// <typeparam name="TProvider">The transient typed fixture provider.</typeparam>
    /// <param name="statusCode">The documented response status.</param>
    /// <param name="name">The unique variant name within that type and status.</param>
    /// <returns>This registration for further mappings.</returns>
    public OpenApiRegistration Response<TResponse, TProvider>(int statusCode = StatusCodes.Status200OK,
        string name = ApiCommonOpenApiConstants.DEFAULT_EXAMPLE_NAME)
        where TResponse : class where TProvider : class, IOpenApiExampleProvider<TResponse>
    {
        // A fixture requires a response body. In particular, 204 must never acquire JSON through documentation.
        if (statusCode is < 200 or > 599 || statusCode == StatusCodes.Status204NoContent || string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException(ApiCommonErrorConstants.OpenApiErrors.INVALID_CONTRACT);
        }

        // Names are scoped to wire type and status, so independent variants cannot overwrite each other.
        if (!responses.TryAdd((typeof(TResponse), statusCode, name), typeof(TProvider)))
        {
            throw new InvalidOperationException(ApiCommonErrorConstants.OpenApiErrors.DUPLICATE_MAPPING);
        }

        RegisterProvider<TProvider>();

        return this;
    }

    /// <summary>
    /// Declares the success and business-error statuses shared by a request/response pair.
    /// </summary>
    /// <typeparam name="TRequest">The native bound request DTO.</typeparam>
    /// <typeparam name="TResponse">The success payload type on the wire.</typeparam>
    /// <param name="successStatus">The actual successful status, including bodyless 204.</param>
    /// <param name="errorStatuses">The operation's explicitly supported business failures.</param>
    /// <returns>This registration for further mappings.</returns>
    public OpenApiRegistration Operation<TRequest, TResponse>(int successStatus = StatusCodes.Status200OK,
        params int[] errorStatuses) where TRequest : class where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(errorStatuses);

        // Mapper support validates the declared error shape; it does not imply every endpoint can return these errors.
        if (successStatus is < 200 or > 299 || errorStatuses.Distinct().Count() != errorStatuses.Length ||
            errorStatuses.Any(status => !status.IsSupportedApiError()))
        {
            throw new InvalidOperationException(ApiCommonErrorConstants.OpenApiErrors.INVALID_CONTRACT);
        }

        // Copy caller-owned arrays: mutating configuration inputs later must not change a published document contract.
        if (!operations.TryAdd((typeof(TRequest), typeof(TResponse)), new(successStatus, [.. errorStatuses])))
        {
            throw new InvalidOperationException(ApiCommonErrorConstants.OpenApiErrors.DUPLICATE_MAPPING);
        }

        // Return the builder only after the operation mapping has been accepted without ambiguity.
        return this;
    }

    /// <summary>
    /// Documents the shared API error boundary that the host separately installs in its runtime pipeline.
    /// </summary>
    /// <returns>This registration for further mappings.</returns>
    public OpenApiRegistration ApiErrorResponses()
    {
        // This declaration is not middleware registration; the host remains responsible for its pipeline.
        apiErrors = true;

        return this;
    }

    /// <summary>
    /// Takes a defensive snapshot so retained builder references cannot mutate document-generation mappings.
    /// </summary>
    /// <returns>An immutable-by-ownership registry containing only types and contract values.</returns>
    internal OpenApiRegistry Build() => new(requests.ToFrozenDictionary(), values.ToFrozenDictionary(), responses.ToFrozenDictionary(),
        operations.ToFrozenDictionary(), apiErrors);

    /// <summary>
    /// Enforces transient provider activation without overriding a conflicting host registration.
    /// </summary>
    /// <typeparam name="TProvider">The explicitly selected provider.</typeparam>
    private void RegisterProvider<TProvider>() where TProvider : class
    {
        // Respect host registration conflicts instead of quietly keeping a singleton that may retain scoped data.
        if (services.Any(descriptor => descriptor.ServiceType == typeof(TProvider) && descriptor.Lifetime != ServiceLifetime.Transient))
        {
            throw new InvalidOperationException(ApiCommonErrorConstants.OpenApiErrors.INVALID_PROVIDER_LIFETIME);
        }

        services.TryAddTransient<TProvider>();
    }
}

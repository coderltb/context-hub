namespace ContextHub.ApiCommon.Filters.Logging;

/// <summary>
/// Records model-bound DTO diagnostics without taking ownership of HTTP bodies, result execution or business failures.
/// </summary>
/// <param name="apiResponseBoundary">The host-selected API scope shared with response adapters.</param>
/// <param name="jsonSerializerService">The existing DI-managed JSON contract.</param>
/// <param name="logger">The logger receiving only masked strings and safe metadata.</param>
public sealed class ApiLoggingFilter(
    ApiResponseBoundary apiResponseBoundary,
    IJsonSerializerService jsonSerializerService,
    ILogger<ApiLoggingFilter> logger) : IAsyncActionFilter, IAsyncAlwaysRunResultFilter, IOrderedFilter
{
    /// <summary>
    /// Surrounds automatic validation and response adapters so the after phase sees their final result.
    /// </summary>
    public int Order => ApiCommonConstants.API_LOGGING_FILTER_ORDER;

    /// <summary>
    /// Captures bound request values before action execution, without reading headers or transport streams.
    /// </summary>
    /// <param name="context">The bound MVC action context.</param>
    /// <param name="next">The native action continuation, deliberately outside diagnostic exception handling.</param>
    /// <returns>The unchanged action execution.</returns>
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (apiResponseBoundary.Contains(context.HttpContext.Request.Path))
        {
            var arguments = context.ActionArguments
                .Where(argument =>
                {
                    var source = context.ActionDescriptor.Parameters.FirstOrDefault(parameter => parameter.Name == argument.Key)
                        ?.BindingInfo?.BindingSource;

                    // A header may be bound under an innocuous CLR name; exclude it before the serializer or masker sees its value.
                    return source != BindingSource.Header && source?.IsFromRequest != false &&
                        argument.Value is not (CancellationToken or HttpContext or HttpRequest or HttpResponse or
                            ClaimsPrincipal or ClaimsIdentity or Claim or IServiceProvider or ActionContext);
                })
                .ToDictionary(argument => argument.Key, argument => argument.Value, StringComparer.Ordinal);
            var multipart = context.HttpContext.Request.ContentType?.StartsWith(ApiCommonConstants.MULTIPART_MEDIA_PREFIX, StringComparison.OrdinalIgnoreCase) == true;
            (string? Value, string? Reason) payload;
            if (context.ActionDescriptor.EndpointMetadata.OfType<ApiMetadataOnlyLoggingAttribute>().Any())
            {
                payload = (null, ApiCommonConstants.Logging.METADATA_ONLY);
            }
            else if (multipart)
            {
                payload = (null, ApiCommonConstants.Logging.MULTIPART);
            }
            else if (arguments.Values.All(value => value is null || IsSupportedType(value.GetType(), [])))
            {
                payload = SerializePayload(arguments);
            }
            else
            {
                payload = (null, ApiCommonConstants.Logging.UNSUPPORTED);
            }

            // Only the redacted copy crosses ILogger; no DTO reference or unmasked snapshot is retained in the request.
            logger.LogInformation(
                ApiCommonLogConstants.PayloadLogs.REQUEST_RECEIVED,
                ActionName(context.ActionDescriptor),
                payload.Value,
                payload.Reason,
                context.HttpContext.Request.ContentLength);
        }

        await next();
    }

    /// <summary>
    /// Records the executed ObjectResult after status/error adaptation and metadata enrichment have completed.
    /// </summary>
    /// <param name="context">The MVC result context.</param>
    /// <param name="next">The native result continuation.</param>
    /// <returns>The unchanged result execution.</returns>
    public async Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next)
    {
        var executed = await next();
        if (!apiResponseBoundary.Contains(context.HttpContext.Request.Path) || executed.Canceled ||
            executed.Exception is not null && !executed.ExceptionHandled)
        {
            return;
        }

        // Files, streams and deferred enumerations remain native results; logging must not consume them a second time.
        var metadataOnly = context.ActionDescriptor.EndpointMetadata.OfType<ApiMetadataOnlyLoggingAttribute>().Any();
        (string? Value, string? Reason) payload = (null, metadataOnly
            ? ApiCommonConstants.Logging.METADATA_ONLY : ApiCommonConstants.Logging.UNSUPPORTED);
        if (!metadataOnly &&
            executed.Result is ObjectResult result &&
            (result.Value is null or ApiResponse<object> { Data: null } || IsSupportedType(result.Value.GetType(), [])))
        {
            payload = SerializePayload(result.Value);
        }

        logger.LogInformation(
            ApiCommonLogConstants.PayloadLogs.RESPONSE_PRODUCED,
            ActionName(context.ActionDescriptor),
            context.HttpContext.Response.StatusCode,
            payload.Value,
            payload.Reason);
    }

    /// <summary>
    /// Creates a bounded redacted copy while isolating only native serialization failures from business execution.
    /// </summary>
    /// <param name="value">The supported DTO or argument dictionary.</param>
    /// <returns>The safe payload, or a metadata-only omission reason.</returns>
    private (string? Value, string? Reason) SerializePayload(object? value)
    {
        try
        {
            var json = jsonSerializerService.Serialize(value);
            var masked = LogMaskingHelper.MaskAllSensitiveData(json);

            // Decide the size limit after redaction, retaining neither a raw fragment nor malformed truncated JSON.
            return masked.Length <= ApiCommonConstants.Logging.MAX_PAYLOAD_LENGTH
                ? (masked, null)
                : (null, ApiCommonConstants.Logging.SIZE_LIMIT);
        }
        catch (NewtonsoftJsonException exception)
        {
            // A DTO getter or serializer contract may fail. Keep the real response intact and never log its exception text.
            logger.LogWarning(ApiCommonLogConstants.PayloadLogs.SERIALIZATION_FAILED, exception.GetType().Name);

            return (null, ApiCommonConstants.Logging.SERIALIZATION_FAILURE);
        }
    }

    /// <summary>
    /// Rejects transport objects and deferred/binary graphs using type metadata without evaluating their getters or enumerators.
    /// </summary>
    /// <param name="type">The runtime value type or declared nested property type.</param>
    /// <param name="visited">Types already examined in this graph, preventing recursive DTO definitions from looping.</param>
    /// <returns>True for scalar values, materialized typed collections and readable DTO properties.</returns>
    private static bool IsSupportedType(
        Type type,
        HashSet<Type> visited)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (IsExcludedType(type))
        {
            return false;
        }

        if (IsScalarType(type))
        {
            return true;
        }

        if (!visited.Add(type))
        {
            // Recursive type definitions are valid; an actual reference loop remains a native serialization failure.
            return true;
        }

        if (type.IsArray)
        {
            return IsSupportedType(type.GetElementType()!, visited);
        }

        var contracts = type.GetInterfaces().Append(type).Where(contract => contract.IsGenericType).ToArray();
        if (contracts.Any(contract => contract.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>)))
        {
            return false;
        }

        var dictionary = contracts.FirstOrDefault(contract =>
            contract.GetGenericTypeDefinition() == typeof(IDictionary<,>) ||
            contract.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));
        if (dictionary is not null)
        {
            return dictionary.GetGenericArguments().All(argument => IsSupportedType(argument, visited));
        }

        var collection = contracts.FirstOrDefault(contract =>
            contract.GetGenericTypeDefinition() == typeof(ICollection<>) ||
            contract.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>));
        if (collection is not null)
        {
            return IsSupportedType(collection.GetGenericArguments()[0], visited);
        }

        if (typeof(IEnumerable).IsAssignableFrom(type) || type is { IsInterface: true } or { IsValueType: false, IsSealed: false })
        {
            // An open DTO member can hide a runtime subtype with streams or deferred enumeration. Omit its diagnostic copy.
            return false;
        }

        // Honor the JSON contract's own ignore marker while avoiding reflection over values or serializer settings copies.
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetMethod is { IsPublic: true, IsStatic: false } &&
                property.GetIndexParameters().Length == 0 && property.GetCustomAttribute<NewtonsoftJsonIgnoreAttribute>() is null)
            .All(property => IsSupportedType(property.PropertyType, visited)) &&
            type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(field => field.GetCustomAttribute<NewtonsoftJsonIgnoreAttribute>() is null)
                .All(field => IsSupportedType(field.FieldType, visited));
    }

    /// <summary>
    /// Excludes runtime, transport and deferred objects before inspecting a diagnostic graph.
    /// </summary>
    /// <param name="type">The non-nullable runtime or declared member type.</param>
    /// <returns>True when the object must not be serialized for payload logging.</returns>
    private static bool IsExcludedType(Type type) =>
        type == typeof(object) || type == typeof(byte[]) || type == typeof(CancellationToken) ||
        typeof(Stream).IsAssignableFrom(type) || typeof(IFormFile).IsAssignableFrom(type) ||
        typeof(HttpContext).IsAssignableFrom(type) || typeof(HttpRequest).IsAssignableFrom(type) ||
        typeof(HttpResponse).IsAssignableFrom(type) || typeof(ClaimsPrincipal).IsAssignableFrom(type) ||
        typeof(ClaimsIdentity).IsAssignableFrom(type) || typeof(Claim).IsAssignableFrom(type) ||
        typeof(Delegate).IsAssignableFrom(type) || typeof(Task).IsAssignableFrom(type) ||
        typeof(IQueryable).IsAssignableFrom(type);

    /// <summary>
    /// Recognizes scalar contracts without evaluating DTO getters or collection enumerators.
    /// </summary>
    /// <param name="type">The non-nullable type being inspected.</param>
    /// <returns>True for the scalar values supported by the existing serializer.</returns>
    private static bool IsScalarType(Type type) =>
        type.IsEnum || Type.GetTypeCode(type) != TypeCode.Object || type == typeof(Guid) ||
        type == typeof(DateTimeOffset) || type == typeof(DateOnly) || type == typeof(TimeOnly) ||
        type == typeof(TimeSpan) || type == typeof(Uri);

    /// <summary>
    /// Uses server-owned action metadata rather than a raw path or consumer-supplied display value.
    /// </summary>
    /// <param name="descriptor">The selected MVC action.</param>
    /// <returns>The controller/action name, or its native descriptor identifier.</returns>
    private static string ActionName(ActionDescriptor descriptor) => descriptor is ControllerActionDescriptor controller
        ? controller.ControllerName + "." + controller.ActionName
        : descriptor.Id;
}


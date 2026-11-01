namespace ContextHub.ApiCommon.Transformers.OpenApi;

/// <summary>
/// Preserves nullable envelope fields and their examples around native schema references.
/// </summary>
/// <param name="apiResponseBoundary">The host-owned API version used in synthetic metadata.</param>
public sealed class ApiEnvelopeSchemaTransformer(ApiResponseBoundary apiResponseBoundary) : IOpenApiSchemaTransformer
{
    /// <summary>
    /// Corrects only the shared envelope metadata after native XML documentation has been applied.
    /// </summary>
    /// <param name="schema">The generated envelope schema.</param>
    /// <param name="context">The native JSON contract used to identify the envelope.</param>
    /// <param name="cancellationToken">Cancellation for document generation.</param>
    /// <returns>The synchronous metadata transformation.</returns>
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(ApiResponse<>) || schema.Properties is null)
        {
            return Task.CompletedTask;
        }

        // Native XML processing drops a null example beside a reference; success samples must retain error: null.
        if (schema.Properties.TryGetValue("error", out var error))
        {
            schema.Properties["error"] = new OpenApiSchema
            {
                Description = error.Description,
                OneOf = error.OneOf?.Any(branch => branch.Type == JsonSchemaType.Null) == true
                    ? [.. error.OneOf]
                    : [error, new OpenApiSchema { Type = JsonSchemaType.Null }],
                Examples = [null!]
            };
        }

        // Keep the native nullable reference intact while supplying the same metadata used by error examples.
        if (schema.Properties.TryGetValue("meta", out var metadata))
        {
            schema.Properties["meta"] = new OpenApiSchema
            {
                AllOf = [metadata],
                Examples = [JsonSerializer.SerializeToNode(
                    ApiErrorExampleFactory.CreateMetadata(apiResponseBoundary.ApiVersion),
                    context.JsonTypeInfo.Options)!]
            };
        }

        return Task.CompletedTask;
    }
}

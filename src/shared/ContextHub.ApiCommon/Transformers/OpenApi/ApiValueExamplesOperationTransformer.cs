namespace ContextHub.ApiCommon.Transformers.OpenApi;

/// <summary>
/// Adds synthetic values only to fields already described by native binding metadata.
/// </summary>
/// <param name="logger">The documentation logger, which never receives fixture contents.</param>
public sealed class ApiValueExamplesOperationTransformer(ILogger<ApiValueExamplesOperationTransformer> logger) : IOpenApiOperationTransformer
{
    /// <summary>
    /// Enriches existing parameters and form properties without creating bodies or modifying shared schemas.
    /// </summary>
    /// <param name="operation">The native operation.</param>
    /// <param name="context">The real binding metadata and scoped provider services.</param>
    /// <param name="cancellationToken">Cancellation for document generation.</param>
    /// <returns>The synchronous enrichment completion.</returns>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OpenApiOperationMetadataHelper.IsApi(context))
        {
            return Task.CompletedTask;
        }

        var registry = context.ApplicationServices.GetService<OpenApiRegistry>();
        var overrides = context.Description.ActionDescriptor.EndpointMetadata.OfType<OpenApiValueExampleAttribute>().ToArray();
        if (overrides.Any(value => string.IsNullOrWhiteSpace(value.Name) || !Enum.IsDefined(value.Target)) ||
            overrides.GroupBy(value => (value.Target, value.Name)).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException(ApiCommonErrorConstants.OpenApiErrors.INVALID_CONTRACT);
        }

        var options = OpenApiOperationMetadataHelper.SerializerOptions(context);
        var fixtures = new Dictionary<Type, JsonObject>();
        var applied = 0;

        foreach (var parameter in context.Description.ParameterDescriptions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = Target(parameter.Source);
            if (target is null || typeof(IFormFile).IsAssignableFrom(parameter.Type) ||
                typeof(IEnumerable<IFormFile>).IsAssignableFrom(parameter.Type))
            {
                continue;
            }

            // Overrides use the actual binding name; the DTO's serialized property name is a separate concern.
            var selected = overrides.SingleOrDefault(value => value.Target == target && value.Name == parameter.Name);
            var example = selected is not null
                ? OpenApiExampleFactory.Create(selected.ProviderType, context.ApplicationServices, options, parameter.Type)
                : ReadValue(parameter, target.Value, registry, fixtures, context.ApplicationServices, options);
            if (example is null)
            {
                continue;
            }

            if (target == OpenApiValueTarget.Form)
            {
                // Native MVC owns form media/schema creation. Copy only affected schema paths before adding examples.
                foreach (var media in operation.RequestBody?.Content?.Values ?? [])
                {
                    var updated = WithFormValue(media.Schema, parameter.Name, example);
                    if (updated is not null)
                    {
                        media.Schema = updated;
                        applied++;
                    }
                }
            }
            else if (operation.Parameters is not null)
            {
                var location = target switch
                {
                    OpenApiValueTarget.Query => ParameterLocation.Query,
                    OpenApiValueTarget.Path => ParameterLocation.Path,
                    _ => ParameterLocation.Header
                };
                for (var index = 0; index < operation.Parameters.Count; index++)
                {
                    var native = operation.Parameters[index];
                    if (native.In == location && native.Name == parameter.Name &&
                        native.CreateShallowCopy() is OpenApiParameter copy)
                    {
                        copy.Example = example.DeepClone();
                        copy.Examples = null;
                        operation.Parameters[index] = copy;
                        applied++;
                    }
                }
            }
        }

        if (applied > 0)
        {
            logger.LogDebug(ApiCommonLogConstants.OpenApiLogs.EXAMPLES_APPLIED, applied);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Maps only real native binding sources; body, services and file bindings are not scalar examples.
    /// </summary>
    /// <param name="source">The source supplied by ApiDescription.</param>
    /// <returns>The supported target, or null for an unrelated binding.</returns>
    private static OpenApiValueTarget? Target(BindingSource? source) => source?.Id switch
    {
        nameof(BindingSource.Query) => OpenApiValueTarget.Query,
        nameof(BindingSource.Path) => OpenApiValueTarget.Path,
        nameof(BindingSource.Header) => OpenApiValueTarget.Header,
        nameof(BindingSource.Form) => OpenApiValueTarget.Form,
        _ => null
    };

    /// <summary>
    /// Gets a bound property's example through the host's JSON contract rather than guessing its name from an alias.
    /// </summary>
    /// <param name="parameter">The native field description.</param>
    /// <param name="target">The declared binding target.</param>
    /// <param name="registry">The optional frozen DTO mappings.</param>
    /// <param name="fixtures">Operation-local synthetic DTOs, not retained runtime state.</param>
    /// <param name="services">The document's provider scope.</param>
    /// <param name="options">The native endpoint JSON options.</param>
    /// <returns>The synthetic field value, or null when no mapped serialized property exists.</returns>
    private static JsonNode? ReadValue(
        ApiParameterDescription parameter,
        OpenApiValueTarget target,
        OpenApiRegistry? registry,
        Dictionary<Type, JsonObject> fixtures,
        IServiceProvider services,
        JsonSerializerOptions options)
    {
        var container = parameter.ModelMetadata.ContainerType;
        var propertyName = parameter.ModelMetadata.PropertyName;
        if (target is not (OpenApiValueTarget.Query or OpenApiValueTarget.Form) || container is null || propertyName is null ||
            registry is null || !registry.Values.TryGetValue(container, out var provider))
        {
            return null;
        }

        if (!fixtures.TryGetValue(container, out var fixture))
        {
            fixture = OpenApiExampleFactory.Create(provider, services, options, container) as JsonObject
                ?? throw new InvalidOperationException(ApiCommonErrorConstants.OpenApiErrors.INVALID_EXAMPLE_TYPE);
            fixtures.Add(container, fixture);
        }

        // JSON metadata includes naming policies and JsonPropertyName. Binding aliases never substitute for CLR identity.
        var jsonProperty = options.GetTypeInfo(container).Properties.FirstOrDefault(property =>
            property.AttributeProvider is PropertyInfo member && member.Name == propertyName);

        return jsonProperty is null ? null : fixture[jsonProperty.Name];
    }

    /// <summary>
    /// Copies the affected form schema path, preserving components and unrelated operations by ownership.
    /// </summary>
    /// <param name="schema">The native form schema or resolved reference.</param>
    /// <param name="name">The exact field binding name.</param>
    /// <param name="example">The detached synthetic field value.</param>
    /// <returns>An operation-local schema when a field matches, otherwise null.</returns>
    private static OpenApiSchema? WithFormValue(
        IOpenApiSchema? schema,
        string name,
        JsonNode example)
    {
        var source = schema is OpenApiSchemaReference reference ? reference.Target : schema;
        if (source?.Properties?.TryGetValue(name, out var property) == true)
        {
            var propertySource = property is OpenApiSchemaReference propertyReference ? propertyReference.Target : property;
            if (source.CreateShallowCopy() is not OpenApiSchema copy || propertySource?.CreateShallowCopy() is not OpenApiSchema field)
            {
                return null;
            }

            field.Examples = [example.DeepClone()];
            copy.Properties = new Dictionary<string, IOpenApiSchema>(source.Properties) { [name] = field };

            return copy;
        }

        // Native multipart composition can place fields inside allOf. Retain siblings and replace only the matched branch.
        if (source?.AllOf is not null)
        {
            for (var index = 0; index < source.AllOf.Count; index++)
            {
                var branch = WithFormValue(source.AllOf[index], name, example);
                if (branch is not null)
                {
                    if (source.CreateShallowCopy() is not OpenApiSchema copy)
                    {
                        return null;
                    }

                    var branches = source.AllOf.ToList();
                    branches[index] = branch;
                    copy.AllOf = branches;

                    return copy;
                }
            }
        }

        return null;
    }
}

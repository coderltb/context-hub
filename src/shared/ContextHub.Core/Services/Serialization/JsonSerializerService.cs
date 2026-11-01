namespace ContextHub.Core.Services.Serialization;

/// <summary>
/// Shares immutable Newtonsoft settings while each operation creates its own native serializer state.
/// </summary>
public sealed class JsonSerializerService : IJsonSerializerService
{
    /// <summary>
    /// Defines the default REST naming convention without accepting type metadata from external payloads.
    /// </summary>
    private readonly JsonSerializerSettings _camel = CreateSettings(new CamelCasePropertyNamesContractResolver());

    /// <summary>
    /// Preserves declared property names while continuing to honor explicit JSON aliases.
    /// </summary>
    private readonly JsonSerializerSettings _declared = CreateSettings(new DefaultContractResolver());

    /// <summary>
    /// Resolves original CLR names for the explicitly selected alias-ignoring variant.
    /// </summary>
    private readonly JsonSerializerSettings _original = CreateSettings(new OriginalPropertyResolver());

    /// <summary>
    /// Produces camel-case JSON without changing global defaults; callers must mask sensitive values before logging.
    /// </summary>
    /// <typeparam name="T">The request or value type serialized by the injected JSON contract.</typeparam>
    /// <param name="value">The value serialized or evaluated by this operation.</param>
    /// <returns>JSON rendered using this operation's declared naming convention.</returns>
    public string Serialize<T>(T value) => JsonConvert.SerializeObject(value, _camel);

    /// <summary>
    /// Preserves declared names only for calls that explicitly request this convention.
    /// </summary>
    /// <typeparam name="T">The request or value type serialized by the injected JSON contract.</typeparam>
    /// <param name="value">The value serialized or evaluated by this operation.</param>
    /// <returns>JSON rendered using this operation's declared naming convention.</returns>
    public string SerializeIgnoreToPascalProperties<T>(T value) => JsonConvert.SerializeObject(value, _declared);

    /// <summary>
    /// Reads the configured camel-case contract; native parse failures propagate to the owning boundary.
    /// </summary>
    /// <typeparam name="T">The result or element type read from the JSON payload.</typeparam>
    /// <param name="json">The JSON text to deserialize; null or blank input produces the documented empty result.</param>
    /// <returns>The deserialized value, or the type's default value for null or blank input.</returns>
    public T? Deserialize<T>(string? json) =>
        string.IsNullOrWhiteSpace(json) ? default : JsonConvert.DeserializeObject<T>(json, _camel);

    /// <summary>
    /// Reads original CLR names without discarding JsonIgnore or other serialization restrictions.
    /// </summary>
    /// <typeparam name="T">The result or element type read from the JSON payload.</typeparam>
    /// <param name="json">The JSON text to deserialize; null or blank input produces the documented empty result.</param>
    /// <returns>The deserialized value, or the type's default value for null or blank input.</returns>
    public T? DeserializeIgnoreProperties<T>(string? json) =>
        string.IsNullOrWhiteSpace(json) ? default : JsonConvert.DeserializeObject<T>(json, _original);

    /// <summary>
    /// Reads declared-name JSON without camel-case conversion.
    /// </summary>
    /// <typeparam name="T">The result or element type read from the JSON payload.</typeparam>
    /// <param name="json">The JSON text to deserialize; null or blank input produces the documented empty result.</param>
    /// <returns>The deserialized value, or the type's default value for null or blank input.</returns>
    public T? DeserializeIgnoreToPascalProperties<T>(string? json) =>
        string.IsNullOrWhiteSpace(json) ? default : JsonConvert.DeserializeObject<T>(json, _declared);

    /// <summary>
    /// Keeps the reference list convenience without returning null collections.
    /// </summary>
    /// <typeparam name="T">The result or element type read from the JSON payload.</typeparam>
    /// <param name="json">The JSON text to deserialize; null or blank input produces the documented empty result.</param>
    /// <returns>The deserialized elements, or an empty list for null or blank input.</returns>
    public List<T> DeserializeList<T>(string? json) => DeserializeIgnoreToPascalProperties<List<T>>(json) ?? [];

    /// <summary>
    /// Converts only JSON parse/contract failures into a boolean; unrelated failures are not swallowed.
    /// </summary>
    /// <typeparam name="T">The result or element type read from the JSON payload.</typeparam>
    /// <param name="json">The JSON text to deserialize; null or blank input produces the documented empty result.</param>
    /// <param name="value">The deserialized value on success, or the type's default value on failure.</param>
    /// <returns>True when non-blank JSON is parsed; false when no usable input or a JSON contract error is encountered.</returns>
    public bool TryDeserialize<T>(
        string? json,
        out T? value)
    {
        try
        {
            value = Deserialize<T>(json);

            return !string.IsNullOrWhiteSpace(json);
        }
        catch (JsonException)
        {
            // External JSON and native exception messages can include private data. Return no diagnostic payload.
            value = default;

            return false;
        }
    }

    /// <summary>
    /// Builds private settings once; native serializers reuse the resolver without accepting external CLR type metadata.
    /// </summary>
    /// <param name="resolver">The Newtonsoft contract resolver defining property naming and metadata behavior.</param>
    /// <returns>Private Newtonsoft settings with safe type-metadata and property-name handling.</returns>
    private static JsonSerializerSettings CreateSettings(IContractResolver resolver) => new()
    {
        ContractResolver = resolver,
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        // Provider-controlled $type fields must never select CLR types during deserialization.
        TypeNameHandling = TypeNameHandling.None,
        MaxDepth = CoreConstants.JsonVariables.MAX_DEPTH
    };

    /// <summary>
    /// Ignores only property name aliases instead of using reflection to replace the complete JSON contract.
    /// </summary>
    private sealed class OriginalPropertyResolver : DefaultContractResolver
    {
        /// <summary>
        /// Keeps native ignore/converter rules while choosing the member's declared CLR name.
        /// </summary>
        /// <param name="member">The CLR member whose JSON property contract is being constructed.</param>
        /// <param name="memberSerialization">The native Newtonsoft member-selection policy.</param>
        /// <returns>The native JSON property contract retaining ignore/converter rules.</returns>
        protected override JsonProperty CreateProperty(
            MemberInfo member,
            MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            // Reuse the native property contract so ignoring aliases cannot re-enable JsonIgnore members or bypass converters.
            property.PropertyName = member.Name;

            return property;
        }
    }
}

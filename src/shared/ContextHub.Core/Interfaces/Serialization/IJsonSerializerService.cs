namespace ContextHub.Core.Interfaces.Serialization;

/// <summary>
/// Provides DI-managed Newtonsoft JSON conventions for shared REST and application serialization.
/// </summary>
public interface IJsonSerializerService
{
    /// <summary>
    /// Serializes camel-case JSON, retaining explicit Newtonsoft property attributes and omitting null members.
    /// </summary>
    /// <typeparam name="T">The request or value type serialized by the injected JSON contract.</typeparam>
    /// <param name="value">The value serialized or evaluated by this operation.</param>
    /// <returns>JSON rendered using this operation's declared naming convention.</returns>
    string Serialize<T>(T value);

    /// <summary>
    /// Preserves declared names and explicit JsonProperty aliases for integrations requiring Pascal-style JSON.
    /// </summary>
    /// <typeparam name="T">The request or value type serialized by the injected JSON contract.</typeparam>
    /// <param name="value">The value serialized or evaluated by this operation.</param>
    /// <returns>JSON rendered using this operation's declared naming convention.</returns>
    string SerializeIgnoreToPascalProperties<T>(T value);

    /// <summary>
    /// Deserializes camel-case JSON; blank input returns default and malformed input raises the native JSON exception.
    /// </summary>
    /// <typeparam name="T">The result or element type read from the JSON payload.</typeparam>
    /// <param name="json">The JSON text to deserialize; null or blank input produces the documented empty result.</param>
    /// <returns>The deserialized value, or the type's default value for null or blank input.</returns>
    T? Deserialize<T>(string? json);

    /// <summary>
    /// Deserializes original CLR property names while ignoring JsonProperty name aliases only.
    /// </summary>
    /// <typeparam name="T">The result or element type read from the JSON payload.</typeparam>
    /// <param name="json">The JSON text to deserialize; null or blank input produces the documented empty result.</param>
    /// <returns>The deserialized value, or the type's default value for null or blank input.</returns>
    T? DeserializeIgnoreProperties<T>(string? json);

    /// <summary>
    /// Deserializes declared-name JSON while honoring explicit aliases and ignore attributes.
    /// </summary>
    /// <typeparam name="T">The result or element type read from the JSON payload.</typeparam>
    /// <param name="json">The JSON text to deserialize; null or blank input produces the documented empty result.</param>
    /// <returns>The deserialized value, or the type's default value for null or blank input.</returns>
    T? DeserializeIgnoreToPascalProperties<T>(string? json);

    /// <summary>
    /// Deserializes a JSON array, returning an empty list for blank or JSON null input.
    /// </summary>
    /// <typeparam name="T">The result or element type read from the JSON payload.</typeparam>
    /// <param name="json">The JSON text to deserialize; null or blank input produces the documented empty result.</param>
    /// <returns>The deserialized elements, or an empty list for null or blank input.</returns>
    List<T> DeserializeList<T>(string? json);

    /// <summary>
    /// Rejects malformed JSON without exposing exception messages or source text in a fallback response.
    /// </summary>
    /// <typeparam name="T">The result or element type read from the JSON payload.</typeparam>
    /// <param name="json">The JSON text to deserialize; null or blank input produces the documented empty result.</param>
    /// <param name="value">The deserialized value on success, or the type's default value on failure.</param>
    /// <returns>True when non-blank JSON is parsed; false when no usable input or a JSON contract error is encountered.</returns>
    bool TryDeserialize<T>(string? json, out T? value);
}

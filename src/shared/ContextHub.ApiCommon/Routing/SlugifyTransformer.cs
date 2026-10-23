namespace ContextHub.ApiCommon.Routing;

/// <summary>
/// Converts MVC controller/action tokens using Core's shared kebab-case rules without changing route parameter values.
/// </summary>
public sealed class SlugifyTransformer : IOutboundParameterTransformer
{
    /// <summary>
    /// Converts the selected outbound token while preserving absent and empty values.
    /// </summary>
    /// <param name="value">The token supplied by the native routing convention.</param>
    /// <returns>The kebab-case token, or null when the supplied value has no text representation.</returns>
    public string? TransformOutbound(object? value)
    {
        // MVC owns token selection; reuse text conversion rather than introducing regex or binding rules here.
        return value?.ToString()?.ToKebabCase();
    }
}

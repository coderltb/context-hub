namespace ContextHub.ApiCommon.Enums;

/// <summary>
/// Identifies the actual native binding location of a scalar example.
/// </summary>
public enum OpenApiValueTarget
{
    /// <summary>
    /// Targets a query parameter rather than a JSON body field.
    /// </summary>
    Query = 0,

    /// <summary>
    /// Targets a declared path parameter.
    /// </summary>
    Path = 1,

    /// <summary>
    /// Targets a declared request header.
    /// </summary>
    Header = 2,

    /// <summary>
    /// Targets an existing non-file form field.
    /// </summary>
    Form = 3
}

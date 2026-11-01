namespace ContextHub.ApiCommon.Attributes.Logging;

/// <summary>
/// Limits MVC diagnostics to operation metadata for endpoints carrying source content or arbitrary imported configuration.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class ApiMetadataOnlyLoggingAttribute : Attribute;

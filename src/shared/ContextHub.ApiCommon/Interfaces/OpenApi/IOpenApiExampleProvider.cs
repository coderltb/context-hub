namespace ContextHub.ApiCommon.Interfaces.OpenApi;

/// <summary>
/// Supplies a synthetic request or response fixture for API documentation, never live application data.
/// </summary>
public interface IOpenApiExampleProvider
{
    /// <summary>
    /// Creates a documentation example using the endpoint's normal JSON serialization settings.
    /// </summary>
    /// <returns>The synthetic request or response value.</returns>
    object GetExample();
}

/// <summary>
/// Supplies a synthetic fixture of the exact request or wire-response type registered by the host.
/// </summary>
/// <typeparam name="T">The documented payload, including its envelope when applicable.</typeparam>
public interface IOpenApiExampleProvider<out T> : IOpenApiExampleProvider where T : class
{
    /// <summary>
    /// Creates a typed fixture without reading live requests, credentials or application data.
    /// </summary>
    /// <returns>The synthetic payload.</returns>
    T CreateExample();

    /// <summary>
    /// Bridges typed providers to the existing explicit example-attribute pipeline.
    /// </summary>
    /// <returns>The same typed fixture as an object.</returns>
    object IOpenApiExampleProvider.GetExample() => CreateExample();
}

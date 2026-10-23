namespace ContextHub.Core.Mappings;

/// <summary>
/// Maps the reusable integration request into transport fields without cloning upload resources.
/// </summary>
public sealed class BaseMappingConfig : IRegister
{
    /// <summary>
    /// Declares transport aliases for the process-wide startup scan without retaining request state.
    /// </summary>
    /// <param name="config">The startup Mapster configuration receiving these mappings.</param>
    public void Register(TypeAdapterConfig config)
    {
        // Stream and content ownership must pass through transport, not through object graph mapping.
        config.NewConfig<BaseThirdPartyApiRequest, BaseHttpRequest>()
            .Map(target => target.RequestUri, source => source.Endpoint)
            .Map(target => target.RequestData, source => source.Content)
            .Map(target => target.RequestFormData, source => source.FormData)
            .Map(target => target.BaseAddress, source => source.BaseUrl)
            .Map(target => target.AdditionalHeaders, source => source.Headers)
            .Map(target => target.AdditionalQueryParams, source => source.QueryParameters)
            .Ignore(target => target.File!)
            .Ignore(target => target.RequestStream!);
    }
}

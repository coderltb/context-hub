namespace ContextHub.Authentication.Application.Mappings.Clients;

/// <summary>
/// Maps use-case contracts and Application state without introducing persistence entities.
/// </summary>
public sealed class ClientMapping : IRegister
{
    /// <summary>
    /// Registers stable contract mappings once through the existing global startup scan.
    /// </summary>
    /// <param name="config">The startup-owned Mapster configuration.</param>
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<CreateClientRequestDto, ClientModel>()
            .Map(value => value.DisplayName, value => value.DisplayName.Trim());

        config.NewConfig<CreateClientRequestDto, IssueClientTokenRequestDto>();

        config.NewConfig<ClientModel, ClientResponseDto>();

        config.NewConfig<ClientTokenModel, ClientTokenResponseDto>();

        config.NewConfig<ClientTokenIssuanceModel, ClientTokenModel>();

        config.NewConfig<ClientTokenIssuanceModel, IssuedClientTokenResponseDto>()
            .Map(value => value.TokenType, _ => AuthenticationConstants.BEARER_TOKEN_TYPE);
    }
}

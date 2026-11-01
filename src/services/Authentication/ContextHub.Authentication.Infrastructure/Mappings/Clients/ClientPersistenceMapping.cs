namespace ContextHub.Authentication.Infrastructure.Mappings.Clients;

/// <summary>
/// Registers client persistence projections without coupling them to Identity user mapping.
/// </summary>
public sealed class ClientPersistenceMapping : IRegister
{
    /// <summary>
    /// Maps approved client fields while native credential generation and audit stamping stay explicit.
    /// </summary>
    /// <param name="config">The startup-owned global mapping configuration.</param>
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Client, ClientResponseDto>()
            .Map(value => value.ClientId, value => value.Id)
            .Map(value => value.DisplayName, value => value.Name)
            .Map(
                value => value.ConcurrencyToken,
                value => Convert.ToBase64String(BitConverter.GetBytes(value.Version)));

        config.NewConfig<ClientToken, ClientTokenResponseDto>()
            .Map(value => value.TokenId, value => value.Id);

        config.NewConfig<Client, ClientModel>()
            .Map(value => value.ClientId, value => value.Id)
            .Map(value => value.DisplayName, value => value.Name)
            .Map(
                value => value.ConcurrencyToken,
                value => Convert.ToBase64String(BitConverter.GetBytes(value.Version)));

        config.NewConfig<ClientModel, Client>()
            .Map(value => value.Id, value => value.ClientId)
            .Map(value => value.Name, value => value.DisplayName)
            .Map(
                value => value.Version,
                value => string.IsNullOrEmpty(value.ConcurrencyToken)
                ? 0U : BitConverter.ToUInt32(Convert.FromBase64String(value.ConcurrencyToken)))
            .Ignore(value => value.Tokens);

        config.NewConfig<ClientToken, ClientTokenModel>()
            .Map(value => value.TokenId, value => value.Id);

        config.NewConfig<ClientTokenModel, ClientToken>()
            .Map(value => value.Id, value => value.TokenId)
            .Ignore(value => value.Client);
    }
}

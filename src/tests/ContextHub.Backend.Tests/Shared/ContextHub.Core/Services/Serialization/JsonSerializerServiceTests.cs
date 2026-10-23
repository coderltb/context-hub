namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Services.Serialization;

/// <summary>
/// Verifies DI-managed Newtonsoft contracts without global state or payload-bearing fallback errors.
/// </summary>
public sealed class JsonSerializerServiceTests
{
    /// <summary>
    /// Honors Newtonsoft attributes, camel-case defaults and the explicit declared-name variant.
    /// </summary>
    [Fact]
    public void Serializer_UsesNewtonsoftContractThroughDi()
    {
        var services = new ServiceCollection();
        services.AddJsonSerializer();
        using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<IJsonSerializerService>();
        var payload = new JsonPayload { Name = "sample", WireValue = "mapped" };

        Assert.Equal("{\"name\":\"sample\",\"wire_value\":\"mapped\"}", serializer.Serialize(payload));
        Assert.Equal("{\"Name\":\"sample\",\"wire_value\":\"mapped\"}", serializer.SerializeIgnoreToPascalProperties(payload));
        Assert.Equal("returned", serializer.Deserialize<JsonPayload>("{\"wire_value\":\"returned\",\"unknown\":true}")!.WireValue);
    }

    /// <summary>
    /// Handles blank/list input and safely rejects malformed JSON while supporting original property names.
    /// </summary>
    [Fact]
    public void Serializer_HandlesBlankInvalidAndOriginalNames()
    {
        var services = new ServiceCollection();
        services.AddJsonSerializer();
        using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<IJsonSerializerService>();

        Assert.Null(serializer.Deserialize<JsonPayload>(" "));
        Assert.Empty(serializer.DeserializeList<JsonPayload>(""));
        Assert.False(serializer.TryDeserialize<JsonPayload>("{private-invalid", out var invalid));
        Assert.Null(invalid);
        Assert.Equal("original", serializer.DeserializeIgnoreProperties<JsonPayload>("{\"WireValue\":\"original\"}")!.WireValue);
        Assert.Equal("sample", serializer.DeserializeIgnoreToPascalProperties<JsonPayload>("{\"Name\":\"sample\"}")!.Name);
    }

    /// <summary>
    /// Keeps one DI serializer while naming variants remain independent under concurrent use.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task Serializer_UsesOneRegistrationWithoutMixingNamingVariants()
    {
        var services = new ServiceCollection();
        services.AddJsonSerializer();
        services.AddRestClients();
        using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<IJsonSerializerService>();
        var payload = new JsonPayload { Name = "sample" };

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(index => Task.Run(() => index % 2 == 0
            ? serializer.Serialize(payload)
            : serializer.SerializeIgnoreToPascalProperties(payload))));

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IJsonSerializerService));
        Assert.Same(serializer, provider.GetRequiredService<IJsonSerializerService>());
        for (var index = 0; index < results.Length; index++)
        {
            Assert.Equal(index % 2 == 0 ? "{\"name\":\"sample\"}" : "{\"Name\":\"sample\"}", results[index]);
        }
    }

    /// <summary>
    /// Ignores only aliases in the original-name variant while keeping native ignore attributes enforced.
    /// </summary>
    [Fact]
    public void OriginalNames_KeepIgnoredMembersExcluded()
    {
        var services = new ServiceCollection().AddJsonSerializer();
        using var provider = services.BuildServiceProvider();

        var result = provider.GetRequiredService<IJsonSerializerService>()
            .DeserializeIgnoreProperties<JsonPayload>("{\"WireValue\":\"original\",\"Ignored\":\"private-value\"}");

        Assert.Equal("original", result!.WireValue);
        Assert.Null(result.Ignored);
    }

    /// <summary>
    /// Carries attribute metadata recognized by the requested Newtonsoft serializer.
    /// </summary>
    public sealed class JsonPayload
    {
        /// <summary>
        /// Gets or sets a convention-named property.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets an explicitly named wire value.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("wire_value")]
        public string? WireValue { get; set; }

        /// <summary>
        /// Gets or sets a member excluded from every JSON naming variant.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string? Ignored { get; set; }
    }
}

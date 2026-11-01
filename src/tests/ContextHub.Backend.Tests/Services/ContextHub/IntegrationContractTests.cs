namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Verifies supported imports, closed schemas and deterministic public-network boundaries.
/// </summary>
public sealed class IntegrationContractTests
{
    /// <summary>
    /// Normalizes client wrappers through the same canonical import path.
    /// </summary>
    /// <param name="wrapper">The supported optional wrapper.</param>
    [Theory]
    [InlineData("")]
    [InlineData("servers")]
    [InlineData("mcpServers")]
    public void Import_AcceptsSupportedWrappers(string wrapper)
    {
        const string connections = "\"docs\":{\"url\":\"https://example.com/mcp\",\"autoApprove\":[\"*\"]}";
        var json = wrapper.Length == 0 ? "{" + connections + "}" : "{\"" + wrapper + "\":{" + connections + "}}";
        using var document = JsonDocument.Parse(json);
        var item = Assert.Single(ExternalMcpImportHelper.Normalize(document.RootElement));
        Assert.Equal("docs", item.Name);
        Assert.Equal("https://example.com/mcp", item.Config.Url);
        Assert.True(item.Config.Enabled);
        Assert.Equal("all", item.Config.ToolPolicy.Mode);
        Assert.Null(item.Config.ToolPolicy.Tools);
    }

    /// <summary>
    /// Keeps an explicit empty tools list distinct from the compatibility alias.
    /// </summary>
    [Fact]
    public void Import_ExplicitToolsOverridesAutoApprove()
    {
        using var document = JsonDocument.Parse("""{"docs":{"url":"https://example.com/mcp","tools":[],"autoApprove":["*"],"disabled":true}}""");
        var item = Assert.Single(ExternalMcpImportHelper.Normalize(document.RootElement));
        Assert.Equal("allowlist", item.Config.ToolPolicy.Mode);
        Assert.Empty(item.Config.ToolPolicy.Tools!);
        Assert.False(item.Config.Enabled);
        Assert.Single(item.Warnings);
    }

    /// <summary>
    /// Rejects unknown import fields and ambiguous duplicate keys before native discovery.
    /// </summary>
    /// <param name="json">The invalid untrusted import.</param>
    [Theory]
    [InlineData("""{"docs":{"url":"https://example.com/mcp","headers":{"Authorization":"secret"}}}""")]
    [InlineData("""{"docs":{"url":"https://user:secret@example.com/mcp"}}""")]
    [InlineData("""{"docs":{"url":"https://example.com/mcp?api_key=secret"}}""")]
    [InlineData("""{"docs":{"url":"https://example.com/mcp","type":"stdio"}}""")]
    [InlineData("""{"docs":{"url":"https://example.com/mcp","tools":["*","other"]}}""")]
    [InlineData("""{"docs":{"url":"https://example.com/mcp","url":"https://other.example/mcp"}}""")]
    public void Import_InvalidInputIsRejected(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Throws<BusinessException>(() => ExternalMcpImportHelper.Normalize(document.RootElement));
    }

    /// <summary>
    /// Checks canonical JSON before the serializer could silently drop an unknown property.
    /// </summary>
    /// <param name="json">The invalid canonical request.</param>
    [Theory]
    [InlineData("""{"schemaVersion":1,"url":"https://example.com/mcp","enabled":true,"toolPolicy":{"mode":"all"},"headers":{}}""")]
    [InlineData("""{"schemaVersion":"1","url":"https://example.com/mcp","enabled":true,"toolPolicy":{"mode":"all"}}""")]
    [InlineData("""{"schemaVersion":1,"url":"https://example.com/mcp","enabled":true,"toolPolicy":{"mode":"all","tools":[]}}""")]
    public void CanonicalConfig_RejectsUnknownFieldsAndWrongTypes(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Throws<BusinessException>(() => ExternalMcpImportHelper.ReadCanonical(document.RootElement));
    }

    /// <summary>
    /// Blocks private and transition-address paths before the actual outbound socket is opened.
    /// </summary>
    /// <param name="address">The DNS result or literal endpoint address.</param>
    /// <param name="expected">Whether public hosted connections may use it.</param>
    [Theory]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.0.0.1", false)]
    [InlineData("172.16.0.1", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("100.64.1.1", false)]
    [InlineData("192.0.2.1", false)]
    [InlineData("::1", false)]
    [InlineData("::ffff:127.0.0.1", false)]
    [InlineData("64:ff9b::7f00:1", false)]
    [InlineData("2002:7f00:1::", false)]
    [InlineData("fc00::1", false)]
    [InlineData("2001:db8::1", false)]
    [InlineData("8.8.8.8", true)]
    [InlineData("2606:4700:4700::1111", true)]
    public void Endpoint_UsesOnlyPublicAddresses(string address, bool expected) =>
        Assert.Equal(expected, ExternalEndpointHelper.IsPublicAddress(System.Net.IPAddress.Parse(address)));

    /// <summary>
    /// Narrows a reviewed schema and refuses network references before evaluation.
    /// </summary>
    [Fact]
    public void ReviewedSchema_IsClosedAndBoundedWithoutReferenceFetch()
    {
        using var source = JsonDocument.Parse("""{"type":"object","properties":{"query":{"type":"string"}},"required":["query"]}""");
        var schema = Assert.IsType<JsonElement>(ExternalToolSchemaHelper.Sanitize(source.RootElement));
        using var allowed = JsonDocument.Parse("""{"query":"rate limiting"}""");
        using var unknown = JsonDocument.Parse("""{"query":"rate limiting","password":"secret"}""");
        using var reference = JsonDocument.Parse("""{"type":"object","$ref":"https://example.com/schema"}""");
        Assert.True(ExternalToolSchemaHelper.IsValid(schema, allowed.RootElement));
        Assert.False(ExternalToolSchemaHelper.IsValid(schema, unknown.RootElement));
        Assert.Null(ExternalToolSchemaHelper.Sanitize(reference.RootElement));
    }

    /// <summary>
    /// Refuses remote output references without mutating the process-global schema registry.
    /// </summary>
    [Fact]
    public void OutputSchema_DoesNotResolveNetworkReferences()
    {
        using var schema = JsonDocument.Parse("""{"type":"object","properties":{"value":{"$ref":"https://example.invalid/private-schema"}}}""");
        using var value = JsonDocument.Parse("""{"value":{}}""");

        Assert.False(ExternalToolSchemaHelper.IsValidOutput(schema.RootElement, value.RootElement));
    }

    /// <summary>
    /// Keeps source paths relative and exact before GitHub receives them.
    /// </summary>
    /// <param name="path">The untrusted authoring directory.</param>
    [Theory]
    [InlineData("../secret")]
    [InlineData("/etc/passwd")]
    [InlineData("folder/../../secret")]
    [InlineData("folder\\secret")]
    [InlineData(".git/objects")]
    public void SourceDirectory_RejectsTraversal(string path) =>
        Assert.Throws<BusinessException>(() => GitHubSourceHelper.EncodeSourcePath(path));

    /// <summary>
    /// Uses advertised documentation versions without inventing an available version.
    /// </summary>
    [Fact]
    public void DocumentationLibrary_RequiresAdvertisedRequestedVersion()
    {
        const string result = "Available Libraries:\n\n- Title: Example\n- Context7-compatible library ID: /owner/example\n- Description: Public docs\n- Versions: v1.2.0, v2.0.0";
        Assert.Equal("/owner/example/v1.2.0", Context7DocumentationHelper.SelectLibrary(result, "Example", "1.2.0")?.Id);
        Assert.Null(Context7DocumentationHelper.SelectLibrary(result, "Example", "3.0.0"));
    }

    /// <summary>
    /// Searches later exact-name matches when the first ranked result lacks the requested version.
    /// </summary>
    [Fact]
    public void DocumentationLibrary_SelectsVersionFromLaterExactMatch()
    {
        const string result = "- Title: ASP.NET Core\n- Context7-compatible library ID: /dotnet/aspnetcore.docs\n- Versions: 218167\n----------\n- Title: ASP.NET Core\n- Context7-compatible library ID: /dotnet/aspnetcore\n- Versions: v8.0.0, v10.0.0";

        Assert.Equal("/dotnet/aspnetcore/v10.0.0", Context7DocumentationHelper.SelectLibrary(result, "ASP.NET Core", "10.0.0")?.Id);
    }
}

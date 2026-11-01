using ContextHub.Api.Mappings.Mcp;
using ContextHub.Application.Constants.Mcp;
using ContextHub.Application.Dtos.Mcp;
using ContextHub.Application.Models.Mcp;
using Mapster;
using ModelContextProtocol.Protocol;

namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Keeps data-only SDK discovery mappings independent of authorization and transport policy.
/// </summary>
public sealed class McpProtocolMappingTests
{
    /// <summary>
    /// Preserves optional argument metadata without publishing stored prompt content.
    /// </summary>
    /// <param name="required">The configured native required flag, including omission.</param>
    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public void PromptMappingPreservesNullableArgumentMetadata(bool? required)
    {
        var configuration = new TypeAdapterConfig();
        new McpProtocolMapping().Register(configuration);
        configuration.Compile();
        var source = new McpPromptModel
        {
            Name = "review_flow",
            Title = "Review",
            Description = "Review the selected package.",
            Content = "Stored guidance is rendered only by prompts/get.",
            Arguments = [new McpPromptArgumentDto
            {
                Name = "package_id",
                Description = "The selected package.",
                Required = required
            }]
        };

        var prompt = source.Adapt<Prompt>(configuration);

        Assert.Equal("review_flow", prompt.Name);
        Assert.Equal("Review", prompt.Title);
        Assert.Equal("Review the selected package.", prompt.Description);
        var argument = Assert.Single(prompt.Arguments!);
        Assert.Equal("package_id", argument.Name);
        Assert.Equal("The selected package.", argument.Description);
        Assert.Equal(required, argument.Required);
        var json = JsonSerializer.SerializeToElement(prompt);
        Assert.False(json.TryGetProperty("content", out _));
    }

    /// <summary>
    /// Retains the approved resource routes and JSON MIME type for native discovery.
    /// </summary>
    [Fact]
    public void ResourceMappingsKeepRoutesAndMimeType()
    {
        var configuration = new TypeAdapterConfig();
        new McpProtocolMapping().Register(configuration);
        configuration.Compile();
        var fixedDefinition = McpResourceConstants.DEFINITIONS.First(value => value.Leaf is null);
        var templateDefinition = McpResourceConstants.DEFINITIONS.First(value => value.Leaf is not null);

        var resource = fixedDefinition.Adapt<Resource>(configuration);
        var template = templateDefinition.Adapt<ResourceTemplate>(configuration);

        Assert.Equal(fixedDefinition.Uri, resource.Uri);
        Assert.Equal(fixedDefinition.Name, resource.Name);
        Assert.Equal("application/json", resource.MimeType);
        Assert.Equal(templateDefinition.Uri, template.UriTemplate);
        Assert.Equal(templateDefinition.Name, template.Name);
        Assert.Equal("application/json", template.MimeType);
    }
}

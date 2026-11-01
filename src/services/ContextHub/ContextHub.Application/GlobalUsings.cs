global using System.Text.Json;
global using System.Text.Json.Nodes;
global using FluentValidation;

global using Mediator;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
global using ContextHub.Core.Exceptions;
global using ContextHub.Core.Interfaces.Authentication;
global using ContextHub.Core.Interfaces.Messaging.Authorization;

global using ContextHub.Shared.Dtos.Responses;
global using ContextHub.Shared.Enums;
global using ContextHub.Application.Interfaces;
global using ContextHub.Application.Policies;
global using ContextHub.Application.Dtos.Catalog;
global using ContextHub.Application.Interfaces.Catalog;
global using ContextHub.Application.Models.Catalog;
global using ContextHub.Application.Dtos.Integration;
global using ContextHub.Application.Interfaces.Integration;
global using ContextHub.Application.Constants.Telemetry;
global using ContextHub.Application.Dtos.Telemetry;
global using ContextHub.Application.Models.Telemetry;
global using ContextHub.Application.Interfaces.Telemetry;
global using ContextHub.Application.Constants.Mcp;
global using ContextHub.Application.Factories.Mcp;

global using ContextHub.Application.Constants.Catalog;
global using ContextHub.Application.Validators.Catalog;
global using ContextHub.Application.Constants.Integration;
global using ContextHub.Application.Extensions.Integration;



global using ContextHub.Application.Commands.Integration;
global using ContextHub.Application.Queries.Integration;


global using ContextHub.Application.Models.Mcp;
global using ContextHub.Application.Enums;
global using CoreAuthorizationException = ContextHub.Core.Exceptions.AuthorizationException;
global using ContextHub.Application.Policies.Telemetry;
global using System.Text.Json.Serialization;

global using ContextHub.Shared.Helpers.Persistence;
global using ContextHub.Shared.Models.Persistence;
global using ContextHub.Shared.Constants;

global using ContextHub.Application.Dtos.Mcp;
global using ContextHub.Application.Interfaces.Mcp;
global using ContextHub.Application.Exceptions.Telemetry;
global using ContextHub.Application.Models.Integration;
global using ContextHub.Application.Commands.Telemetry;

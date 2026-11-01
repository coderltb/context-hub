global using System.ComponentModel.DataAnnotations;
global using System.Security.Claims;

global using FluentValidation;
global using Mapster;
global using Mediator;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;

global using ContextHub.Authentication.Application.Constants;
global using ContextHub.Authentication.Application.Dtos.Accounts;
global using ContextHub.Authentication.Application.Dtos.Clients;
global using ContextHub.Authentication.Application.Dtos.Roles;
global using ContextHub.Authentication.Application.Dtos.Users;
global using ContextHub.Authentication.Application.Enums;
global using ContextHub.Authentication.Application.Extensions;
global using ContextHub.Authentication.Application.Interfaces.Accounts;
global using ContextHub.Authentication.Application.Interfaces.Clients;
global using ContextHub.Authentication.Application.Interfaces.Roles;
global using ContextHub.Authentication.Application.Interfaces.Sessions;
global using ContextHub.Authentication.Application.Interfaces.Users;
global using ContextHub.Authentication.Application.Models.Accounts;
global using ContextHub.Authentication.Application.Models.Clients;
global using ContextHub.Shared.Helpers.Persistence;
global using ContextHub.Shared.Models.Persistence;
global using ContextHub.Authentication.Application.Models.Sessions;
global using ContextHub.Authentication.Application.Models.Users;
global using ContextHub.Authentication.Application.Policies;

global using ContextHub.Core.Constants;
global using ContextHub.Core.Exceptions;
global using ContextHub.Core.Interfaces.Authentication;
global using ContextHub.Core.Interfaces.Messaging.Authorization;

global using ContextHub.Shared.Dtos.Responses;
global using ContextHub.Shared.Enums;

global using ContextHub.Shared.Constants;

global using JetBrains.Annotations;

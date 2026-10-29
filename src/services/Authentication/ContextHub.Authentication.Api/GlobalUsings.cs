// Global using directives

global using ContextHub.ApiCommon.Attributes.OpenApi;

global using ContextHub.ApiCommon.DependencyInjection;
global using ContextHub.ApiCommon.Extensions;
global using ContextHub.Observability.DependencyInjection;
global using ContextHub.Observability.Extensions;
global using ContextHub.Authentication.Api.DependencyInjection;
global using ContextHub.Authentication.Api.Extensions;
global using ContextHub.Authentication.Api.Transformers.OpenApi;
global using ContextHub.Authentication.Application.DependencyInjection;
global using AuthenticationServiceOptions = ContextHub.Authentication.Infrastructure.Options.AuthenticationOptions;
global using Microsoft.AspNetCore.Cors.Infrastructure;
global using Microsoft.AspNetCore.OpenApi;
global using Microsoft.OpenApi;
global using Microsoft.Extensions.Options;
global using ContextHub.Authentication.Application.Dtos.Clients;
global using ContextHub.Authentication.Application.Dtos.Users;
global using ContextHub.Authentication.Application.Dtos.Roles;
global using ContextHub.Authentication.Application.Constants;
global using ContextHub.Authentication.Api.Constants;
global using Mediator;
global using ContextHub.Authentication.Application.Queries.Clients.GetClients;
global using ContextHub.Authentication.Application.Queries.Clients.GetClient;
global using ContextHub.Authentication.Application.Commands.Clients.CreateClient;
global using ContextHub.Authentication.Application.Commands.Clients.UpdateClient;
global using ContextHub.Authentication.Application.Commands.Clients.IssueClientToken;
global using ContextHub.Authentication.Application.Commands.Clients.RevokeClientToken;
global using ContextHub.Authentication.Application.Queries.Users.GetUsers;
global using ContextHub.Authentication.Application.Queries.Users.GetUser;
global using ContextHub.Authentication.Application.Queries.Roles.GetRoles;
global using ContextHub.Authentication.Application.Commands.Users.CreateUser;
global using ContextHub.Authentication.Application.Commands.Users.SetUserStatus;
global using ContextHub.Authentication.Application.Commands.Users.RegenerateInvitation;
global using ContextHub.Authentication.Application.Commands.Roles.ReplaceUserRoles;
global using ContextHub.Authentication.Infrastructure.DependencyInjection;
global using ContextHub.Core.DependencyInjection;
global using ContextHub.Shared.Dtos.Responses;
global using Microsoft.AspNetCore.Authorization;
global using Microsoft.AspNetCore;
global using Microsoft.AspNetCore.Authentication;
global using Microsoft.AspNetCore.Identity;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.Mvc.RazorPages;
global using ContextHub.Authentication.Api.Models.Accounts;
global using Microsoft.AspNetCore.WebUtilities;
global using System.ComponentModel.DataAnnotations;
global using System.Security.Claims;
global using OpenIddict.Abstractions;
global using OpenIddict.Server.AspNetCore;
global using static OpenIddict.Abstractions.OpenIddictConstants;

global using ContextHub.ApiCommon.Options;
global using ContextHub.ApiCommon.Health;
global using ContextHub.ApiCommon.Constants;
global using Microsoft.AspNetCore.Diagnostics.HealthChecks;
global using ContextHub.Authentication.Application.Enums;
global using ContextHub.Authentication.Application.Models.Sessions;
global using ContextHub.Authentication.Application.Commands.Sessions.CreateAuthorizationGrant;
global using ContextHub.Authentication.Application.Commands.Sessions.RevokeSession;
global using ContextHub.Authentication.Application.Queries.Sessions.ValidateTokenGrant;

global using System.Diagnostics.CodeAnalysis;

global using ContextHub.Authentication.Api.Enums;
global using ContextHub.Authentication.Api.Filters;
global using Microsoft.AspNetCore.Mvc.Filters;

global using ContextHub.Authentication.Application.Dtos.Accounts;

global using ContextHub.Authentication.Application.Commands.Users.InviteUser;


global using ContextHub.Authentication.Application.Commands.Accounts.ForgotPassword;

global using ContextHub.Authentication.Application.Commands.Accounts.VerifyPasswordResetOtp;

global using ContextHub.Authentication.Application.Commands.Accounts.ResetPassword;

global using ContextHub.Authentication.Application.Queries.Users.GetCurrentUser;
global using ContextHub.Authentication.Application.Commands.Users.ChangePassword;

global using ContextHub.ApiCommon.Controllers;

global using ContextHub.Authentication.Infrastructure.Constants;

global using ContextHub.Authentication.Application.Commands.Accounts.Login;
global using ContextHub.Authentication.Application.Commands.Accounts.AcceptInvitation;
global using Microsoft.AspNetCore.HttpOverrides;
global using System.Net;

global using ContextHub.Authentication.Api.Pages.Shared;

global using ContextHub.ApiCommon.Interfaces.OpenApi;
global using ContextHub.Authentication.Api.Examples.Users;
global using ContextHub.Authentication.Api.Examples.Clients;

global using Swashbuckle.AspNetCore.SwaggerUI;

global using ContextHub.ApiCommon.Factories.OpenApi;

global using System.Collections.Frozen;

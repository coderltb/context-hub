// Global using directives
global using ContextHub.Backend.Tests.Services.Authentication.Fixtures;
global using System.Globalization;
global using ContextHub.Authentication.Api.Controllers.Admin;
global using ContextHub.Authentication.Api.Controllers.Oidc;
global using OpenIddict.Server.AspNetCore;

global using ContextHub.Authentication.Api.DependencyInjection;
global using ContextHub.Authentication.Api.Extensions;
global using Microsoft.AspNetCore.Razor.Hosting;
global using ContextHub.Authentication.Application.DependencyInjection;
global using ContextHub.Authentication.Application.Interfaces.Accounts;
global using ContextHub.Authentication.Application.Interfaces.Clients;

global using ContextHub.Authentication.Application.Interfaces.Sessions;
global using ContextHub.Authentication.Application.Interfaces.Users;
global using ContextHub.Authentication.Infrastructure.Interfaces.Repositories.Clients;
global using ContextHub.Authentication.Infrastructure.Interfaces.Repositories.Users;
global using Microsoft.AspNetCore.WebUtilities;

global using ContextHub.Authentication.Infrastructure.Services.Clients;
global using AuthenticationClientEntity = ContextHub.Authentication.Domain.Entities.Client;
global using AuthenticationClientTokenEntity = ContextHub.Authentication.Domain.Entities.ClientToken;
global using ContextHub.Authentication.Infrastructure.Repositories.Clients;
global using ContextHub.Core.Repositories;
global using ContextHub.Core.Interfaces.Persistence;
global using ContextHub.Backend.Tests.Shared.ContextHub.Core.Mappings;
global using ContextHub.Authentication.Application.Dtos.Clients;
global using ContextHub.Authentication.Application.Dtos.Users;

global using ContextHub.Authentication.Application.Commands.Users.InviteUser;
global using ContextHub.Authentication.Application.Commands.Roles.ReplaceUserRoles;

global using ContextHub.Authentication.Application.Commands.Clients.CreateClient;
global using ContextHub.Authentication.Application.Queries.Clients.GetClients;
global using ContextHub.Authentication.Infrastructure.Context;
global using ContextHub.Authentication.Domain.Entities;
global using ContextHub.Authentication.Infrastructure.Interceptors;
global using ContextHub.Authentication.Infrastructure.DependencyInjection;

global using ContextHub.Core.Services.Email;



global using ContextHub.Authentication.Infrastructure.Services.Users;

global using ContextHub.Authentication.Infrastructure.Handlers.Introspection;
global using ContextHub.Authentication.Application.Models.Clients;
global using OpenIddict.Abstractions;
global using ClientTokenValidationContext = OpenIddict.Server.OpenIddictServerEvents.ValidateTokenContext;
global using OpenIddict.Server;
global using AuthenticationServiceOptions = ContextHub.Authentication.Infrastructure.Options.AuthenticationOptions;
global using ContextHub.Core.Queries;
global using System.Linq.Expressions;
global using Microsoft.EntityFrameworkCore.Diagnostics;
global using Microsoft.EntityFrameworkCore.Infrastructure;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.FileProviders;
global using Microsoft.AspNetCore.Authentication.Cookies;
global using Microsoft.AspNetCore.Identity;
global using ConnectionState = System.Data.ConnectionState;
global using System.Data.Common;
global using Microsoft.EntityFrameworkCore.Storage;
global using ContextHub.Shared.Helpers.Security;

global using Dapper;
global using Npgsql;
global using StackExchange.Redis;
global using RedisDatabase = StackExchange.Redis.IDatabase;
global using ContextHub.Core.Exceptions;
global using ContextHub.Shared.Enums;
global using ContextHub.ApiCommon.Exceptions;

global using ContextHub.Shared.Interfaces.Auditing;
global using ContextHub.Core.Interfaces.Auditing;
global using ContextHub.Core.Interceptors.Auditing;

global using ContextHub.Core.Interfaces.Authentication;
global using ContextHub.Core.Interfaces.ClientDevices;
global using ContextHub.Shared.Models.ClientDevices;
global using ContextHub.ApiCommon.Options;
global using ContextHub.ApiCommon.Services.Authentication;
global using CoreAuthenticationException = ContextHub.Core.Exceptions.AuthenticationException;

global using ContextHub.Core.Interfaces.Caches;
global using ContextHub.Core.Interfaces.Concurrency;
global using Microsoft.Extensions.Caching.Hybrid;
global using Microsoft.Extensions.Caching.Distributed;

global using ContextHub.Shared.Dtos.Options.Persistence;
global using ContextHub.Core.Helpers.Persistence;
global using ContextHub.Core.Services.Persistence;

global using ContextHub.Shared.Helpers.Conversion;
global using ContextHub.Shared.Helpers.Time;

global using System.Collections.Concurrent;
global using System.ComponentModel.DataAnnotations;
global using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;
global using System.Net;
global using System.Diagnostics;
global using ContextHub.ApiCommon.Handlers.Http;
global using ContextHub.Observability.Constants;
global using ContextHub.Shared.Helpers.Logging;
global using ContextHub.Shared.Constants;
global using ContextHub.Observability.Formatters;
global using ContextHub.Observability.DependencyInjection;
global using ContextHub.Observability.Extensions;
global using ContextHub.Observability.Options;
global using ContextHub.Observability.Processors;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.Options;
global using OpenTelemetry;
global using OpenTelemetry.Trace;
global using OpenTelemetry.Metrics;
global using Serilog;
global using Serilog.Core;
global using Serilog.Events;
global using Serilog.Parsing;
global using System.Text;
global using System.Text.Json;
global using System.Text.RegularExpressions;
global using ContextHub.ApiCommon.Attributes.OpenApi;
global using ContextHub.ApiCommon.Enums;
global using ContextHub.ApiCommon.Interfaces.OpenApi;
global using ContextHub.ApiCommon.DependencyInjection.OpenApi;
global using ContextHub.ApiCommon.Constants;
global using ContextHub.ApiCommon.DependencyInjection;
global using ContextHub.ApiCommon.Extensions;
global using ContextHub.ApiCommon.Factories;
global using ContextHub.ApiCommon.Handlers.Exceptions;
global using ContextHub.ApiCommon.Factories.OpenApi;
global using Microsoft.AspNetCore.Diagnostics;
global using ContextHub.ApiCommon.Filters.Responses;
global using Microsoft.AspNetCore.Mvc.Filters;
global using Microsoft.AspNetCore.Http.Features;
global using ContextHub.ApiCommon.Middlewares;
global using ContextHub.Cache.DependencyInjection;
global using ContextHub.Cache.Interfaces;
global using ContextHub.Cache.Models;
global using ContextHub.Cache.Services;
global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Hosting;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.Mvc.Controllers;
global using System.Reflection;
global using ContextHub.ApiCommon.Transformers.Routing;
global using Microsoft.AspNetCore.TestHost;
global using Microsoft.AspNetCore.Authentication;
global using Microsoft.AspNetCore.Authorization;
global using Microsoft.AspNetCore.RateLimiting;
global using System.Security.Claims;
global using System.Security.Cryptography;

global using ContextHub.Backend.Tests.Common.Logging;
global using ContextHub.Backend.Tests.Common.Hosting;
global using ContextHub.Backend.Tests.Shared.ContextHub.ApiCommon.Transformers.OpenApi;
global using ContextHub.Backend.Tests.Shared.ContextHub.Core.Fixtures.Persistence;
global using ContextHub.Backend.Tests.Shared.ContextHub.Core.Repositories;
global using ContextHub.Core.Behaviors;
global using ContextHub.Core.Interfaces.Messaging.Authorization;
global using CoreAuthorizationException = ContextHub.Core.Exceptions.AuthorizationException;
global using ContextHub.Core.Constants;
global using ContextHub.Core.DependencyInjection;
global using ContextHub.Shared.Extensions;
global using ContextHub.Core.Extensions.Validations;
global using ContextHub.Shared.Dtos.Requests;
global using ContextHub.Shared.Dtos.Responses;
global using CoreValidationException = ContextHub.Core.Exceptions.ValidationException;
global using FluentValidation;
global using Mediator;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Logging.Abstractions;
global using Testcontainers.PostgreSql;
global using Testcontainers.Redis;
global using Xunit;
global using ContextHub.Core.Interfaces.Http;
global using ContextHub.Core.Services.Http;
global using Mapster;
global using MapsterMapper;
global using Microsoft.Extensions.Diagnostics.HealthChecks;
global using Microsoft.AspNetCore.Diagnostics.HealthChecks;
global using ContextHub.ApiCommon.Health;
global using System.Net.Http.Json;
global using System.Net.Http.Headers;
global using MicrosoftLogger = Microsoft.Extensions.Logging.ILogger;
global using ContextHub.Core.Interfaces.Serialization;

global using ContextHub.Authentication.Application.Enums;
global using ContextHub.Authentication.Application.Models.Sessions;
global using ContextHub.Authentication.Application.Models.Users;
global using ContextHub.Authentication.Application.Commands.Sessions.CreateAuthorizationGrant;
global using ContextHub.Authentication.Application.Commands.Sessions.RevokeSession;
global using ContextHub.Authentication.Application.Queries.Sessions.ValidateTokenGrant;

global using IsolationLevel = System.Data.IsolationLevel;

global using ContextHub.Authentication.Application.Constants;
global using ContextHub.Authentication.Api.Enums;
global using ContextHub.Authentication.Api.Filters;
global using Microsoft.AspNetCore.Mvc.Abstractions;
global using Microsoft.AspNetCore.Routing;

global using ContextHub.ApiCommon.Filters.Logging;
global using Microsoft.AspNetCore.Mvc.ModelBinding;
global using NewtonsoftJsonPropertyAttribute = Newtonsoft.Json.JsonPropertyAttribute;

global using System.Collections;
global using Serilog.Extensions.Logging;

global using SystemTextJsonIgnoreAttribute = System.Text.Json.Serialization.JsonIgnoreAttribute;

global using LoggingFixtureAuthenticationHandler = ContextHub.Backend.Tests.Shared.ContextHub.ApiCommon.DependencyInjection.OpenApiRegistrationTests.FixtureAuthenticationHandler;

global using ContextHub.Authentication.Application.Dtos.Accounts;
global using ContextHub.Authentication.Application.Models.Accounts;



global using SendGrid;
global using SendGridOptions = ContextHub.Shared.Dtos.Options.Email.SendGridOptions;
global using Microsoft.Extensions.DependencyInjection.Extensions;

global using NativeAuthenticationToken = OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreToken<System.Guid>;

global using ContextHub.Authentication.Infrastructure.Repositories.Users;

global using ContextHub.Authentication.Application.Interfaces.Identity;

global using ContextHub.Authentication.Infrastructure.Helpers.Clients;

global using ContextHub.Core.Interfaces.Email;
global using ContextHub.Shared.Dtos.Requests.Emails;

global using ContextHub.Authentication.Application.Queries.Users.GetCurrentUser;
global using ContextHub.Authentication.Application.Commands.Users.ChangePassword;

global using ContextHub.Shared.Models.Validation;



global using Quartz;


global using OpenIddict.Quartz;

global using ContextHub.Observability.Enums;

global using Microsoft.Extensions.Time.Testing;
global using NetArchTest.Rules;

global using NativeAuthenticationAuthorization = OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreAuthorization<System.Guid>;

global using ContextHub.Authentication.Application.Queries.Users.GetUsers;
global using ContextHub.Authentication.Api.Pages.Identity.Account;
global using OpenIddict.Validation;


global using System.Net.Sockets;
global using ContextHub.Authentication.Application.Commands.Accounts.AcceptInvitation;
global using ContextHub.Authentication.Application.Commands.Accounts.Login;

global using Microsoft.IdentityModel.Tokens;
global using Microsoft.IdentityModel.JsonWebTokens;
global using Microsoft.AspNetCore.DataProtection;

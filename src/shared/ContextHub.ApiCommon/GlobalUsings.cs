// Global using directives

global using System.Collections.Frozen;
global using System.Collections;
global using System.Diagnostics;
global using System.Globalization;
global using System.Reflection;
global using System.Security.Claims;
global using System.Text.Json.Nodes;

global using System.Text.Json;

global using Mediator;
global using Microsoft.AspNetCore.Authorization;
global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Diagnostics;
global using Microsoft.AspNetCore.Http.Metadata;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Mvc.Abstractions;
global using Microsoft.AspNetCore.Mvc.ApiExplorer;
global using Microsoft.AspNetCore.Mvc.ApplicationModels;
global using Microsoft.AspNetCore.Mvc.Controllers;
global using Microsoft.AspNetCore.Mvc.Filters;
global using Microsoft.AspNetCore.Mvc.ModelBinding;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.OpenApi;
global using Microsoft.AspNetCore.RateLimiting;
global using Microsoft.AspNetCore.Routing;
global using Microsoft.AspNetCore.WebUtilities;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Diagnostics.HealthChecks;
global using Microsoft.Extensions.Http.Resilience;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using Microsoft.OpenApi;

global using ContextHub.ApiCommon.Attributes.OpenApi;
global using ContextHub.ApiCommon.Configurations.OpenApi;
global using ContextHub.ApiCommon.DependencyInjection.OpenApi;
global using ContextHub.ApiCommon.Constants;
global using ContextHub.ApiCommon.Enums;
global using ContextHub.ApiCommon.Exceptions;
global using ContextHub.ApiCommon.Extensions;
global using ContextHub.ApiCommon.Factories.OpenApi;
global using ContextHub.ApiCommon.Factories;
global using ContextHub.ApiCommon.Filters.Logging;
global using ContextHub.ApiCommon.Filters.Responses;
global using ContextHub.ApiCommon.Handlers.Exceptions;
global using ContextHub.ApiCommon.Handlers.Http;
global using ContextHub.ApiCommon.Helpers.OpenApi;
global using ContextHub.ApiCommon.Interfaces.OpenApi;
global using ContextHub.ApiCommon.Middlewares;
global using ContextHub.ApiCommon.Options;
global using ContextHub.ApiCommon.Services.Authentication;
global using ContextHub.ApiCommon.Services.ClientDevices;
global using ContextHub.ApiCommon.Transformers.OpenApi;
global using ContextHub.ApiCommon.Transformers.Routing;
global using ContextHub.Core.Constants;
global using ContextHub.Core.DependencyInjection;
global using ContextHub.Core.Exceptions;
global using ContextHub.Core.Interfaces.Auditing;
global using ContextHub.Core.Interfaces.Authentication;
global using ContextHub.Core.Interfaces.ClientDevices;

global using ContextHub.Core.Interfaces.Serialization;

global using ContextHub.Shared.Constants;
global using ContextHub.Shared.Dtos.Responses;
global using ContextHub.Shared.Enums;
global using ContextHub.Shared.Extensions;
global using ContextHub.Shared.Helpers.Conversion;
global using ContextHub.Shared.Helpers.Logging;
global using ContextHub.Shared.Interfaces.Responses;
global using ContextHub.Shared.Models.ClientDevices;

global using CoreAuthenticationException = ContextHub.Core.Exceptions.AuthenticationException;
global using CoreAuthorizationException = ContextHub.Core.Exceptions.AuthorizationException;
global using CoreValidationException = ContextHub.Core.Exceptions.ValidationException;
global using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
global using NewtonsoftJsonException = Newtonsoft.Json.JsonException;
global using NewtonsoftJsonIgnoreAttribute = Newtonsoft.Json.JsonIgnoreAttribute;

global using Swashbuckle.AspNetCore.SwaggerUI;
global using System.IO;

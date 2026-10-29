// Global using directives



global using System.Data.Common;
global using System.Data;

global using System.Linq.Expressions;
global using System.Net.Http.Headers;

global using System.Reflection;
global using System.Runtime.CompilerServices;
global using System.Security.Claims;

global using System.Text;

global using Dapper;
global using FluentValidation.Results;
global using FluentValidation;
global using Mapster.Models;
global using Mapster;
global using Mediator;
global using Microsoft.EntityFrameworkCore.Diagnostics;
global using Microsoft.EntityFrameworkCore.Storage;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using Newtonsoft.Json.Serialization;
global using Newtonsoft.Json;
global using Npgsql;
global using SendGrid.Helpers.Mail;
global using SendGrid;

global using ContextHub.Core.Behaviors;
global using ContextHub.Core.Constants;


global using ContextHub.Core.Factories.Persistence;
global using ContextHub.Core.Interceptors.Auditing;
global using ContextHub.Core.Interfaces.Auditing;
global using ContextHub.Core.Interfaces.Email;
global using ContextHub.Core.Interfaces.Http;
global using ContextHub.Core.Interfaces.Messaging.Authorization;
global using ContextHub.Core.Interfaces.Persistence;
global using ContextHub.Core.Interfaces.Serialization;
global using ContextHub.Core.Queries;
global using ContextHub.Core.Repositories;
global using ContextHub.Core.Services.Email;
global using ContextHub.Core.Services.Http;
global using ContextHub.Core.Services.Persistence;
global using ContextHub.Core.Services.Serialization;
global using ContextHub.Shared.Constants;
global using ContextHub.Shared.Dtos.Options.Email;
global using ContextHub.Shared.Dtos.Options.Persistence;
global using ContextHub.Shared.Dtos.Requests.Emails;
global using ContextHub.Shared.Dtos.Requests;
global using ContextHub.Shared.Dtos.Responses;
global using ContextHub.Shared.Enums;
global using ContextHub.Shared.Extensions;
global using ContextHub.Shared.Helpers.Logging;
global using ContextHub.Shared.Interfaces.Auditing;
global using ContextHub.Shared.Models.ClientDevices;
global using ContextHub.Shared.Models.Validation;

global using CoreAuthorizationException = ContextHub.Core.Exceptions.AuthorizationException;
global using CoreValidationException = ContextHub.Core.Exceptions.ValidationException;


global using Microsoft.Extensions.Http.Resilience;
global using Polly.CircuitBreaker;
global using Polly.Timeout;

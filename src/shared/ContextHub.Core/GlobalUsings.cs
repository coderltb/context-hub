// Global using directives
global using ContextHub.Core.Enums;
global using ContextHub.Core.Persistence.Queries;
global using ContextHub.Shared.Dtos.Responses;
global using ContextHub.Core.Models.ClientDevices;
global using System.Runtime.CompilerServices;

global using Microsoft.EntityFrameworkCore.Diagnostics;
global using ContextHub.Shared.Interfaces.Auditing;
global using ContextHub.Core.Interfaces.Auditing;
global using ContextHub.Core.Persistence.Interceptors;

global using System.Security.Claims;

global using System.Data;
global using System.Data.Common;
global using Dapper;
global using Npgsql;
global using Microsoft.EntityFrameworkCore.Storage;
global using ContextHub.Core.Persistence.Options;
global using ContextHub.Core.Persistence.Factories;
global using ContextHub.Core.Persistence.Services;

global using ContextHub.Core.Extensions;

global using System.Security.Cryptography;
global using System.Buffers.Text;

global using System.ComponentModel;
global using DisplayAttribute = System.ComponentModel.DataAnnotations.DisplayAttribute;
global using System.Reflection;
global using System.Linq.Expressions;
global using System.Globalization;
global using System.Text;
global using ContextHub.Core.Messaging.Behaviors;
global using ContextHub.Core.Messaging.Authorization;
global using CoreAuthorizationException = ContextHub.Core.Messaging.Authorization.AuthorizationException;
global using ContextHub.Core.Constants;
global using ContextHub.Core.Persistence.Interfaces;
global using ContextHub.Core.Persistence.Repositories;
global using ContextHub.Core.Persistence.UnitOfWork;
global using ContextHub.Core.Validation;
global using CoreValidationException = ContextHub.Core.Validation.ValidationException;
global using FluentValidation;
global using FluentValidation.Results;
global using Mediator;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Microsoft.Extensions.Logging;
global using ContextHub.Core.Interfaces.Http;
global using ContextHub.Core.Services.Http;
global using Mapster;
global using Mapster.Models;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.Options;
global using ContextHub.Core.Interfaces.Serialization;
global using ContextHub.Core.Services.Serialization;
global using Newtonsoft.Json;
global using Newtonsoft.Json.Serialization;
global using ContextHub.Shared.Dtos.Requests;
global using ContextHub.Shared.Constants;
global using ContextHub.Shared.Helpers;
global using ContextHub.Core.DependencyInjection;
global using System.Net;
global using System.Net.Http.Headers;

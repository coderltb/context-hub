// Global using directives

global using System.Diagnostics;
global using System.Globalization;




global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Http;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using OpenTelemetry.Metrics;
global using OpenTelemetry.Resources;
global using OpenTelemetry.Trace;
global using OpenTelemetry;
global using Serilog.Core;
global using Serilog.Events;
global using Serilog.Formatting.Compact;
global using Serilog.Formatting.Display;
global using Serilog.Formatting;
global using Serilog;

global using ContextHub.Observability.Constants;
global using ContextHub.Observability.Enrichers;
global using ContextHub.Observability.Enums;
global using ContextHub.Observability.Formatters;
global using ContextHub.Observability.Options;
global using ContextHub.Observability.Processors;
global using ContextHub.Shared.Constants;
global using ContextHub.Shared.Helpers.Logging;

global using ILogger = Serilog.ILogger;

global using JetBrains.Annotations;

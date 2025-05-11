// ReSharper disable All

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Nihlen.Common.Telemetry;

// https://docs.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs
public static class TelemetryExtensions
{
    /// <summary>
    /// Setup OpenTelemetry tracing and metrics to the specified endpoint for ASP.NET Core projects.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="serviceName">Service name (ex. Nihlen.Common)</param>
    /// <param name="serviceVersion">Service version (ex. 1.0.0)</param>
    /// <param name="otlpEndpoint">The OpenTelemetry protocol endpoint URL. Default is http://localhost:4317 with gRPC protocol.</param>
    /// <returns></returns>
    public static IServiceCollection? AddCustomTelemetry(this IServiceCollection? services, string? serviceName = null, string? serviceVersion = null, string? otlpEndpoint = null)
    {
        if (services is null)
            return null;

        // var resourceBuilder = Telemetry.GetResourceBuilder(ref serviceName, ref serviceVersion, ref otlpEndpoint);

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName, serviceVersion, otlpEndpoint))
            .WithTracing(b => b
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri(otlpEndpoint);
                    o.Protocol = OtlpExportProtocol.Grpc;
                })
                .AddAspNetCoreInstrumentation(o => { o.RecordException = true; })
                .AddHttpClientInstrumentation(o => { o.RecordException = true; })
                .AddSqlClientInstrumentation(o =>
                {
                    o.SetDbStatementForText = true;
                    o.SetDbStatementForStoredProcedure = true;
                    o.EnableConnectionLevelAttributes = true;
                    o.RecordException = true;
                })
                .AddEntityFrameworkCoreInstrumentation(o =>
                {
                    o.SetDbStatementForText = true;
                    o.SetDbStatementForStoredProcedure = true;
                }))
            .WithMetrics(b => b
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri(otlpEndpoint);
                    o.Protocol = OtlpExportProtocol.Grpc;
                })
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()

                // Not stable yet, seems to cause some memory leaks
                // .AddEventCounterMetrics(options =>
                // {
                //     options.RefreshIntervalSecs = 10;
                // })
                .AddRuntimeInstrumentation()
            );

        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.Configure(options => { options.ActivityTrackingOptions = ActivityTrackingOptions.SpanId | ActivityTrackingOptions.TraceId | ActivityTrackingOptions.ParentId | ActivityTrackingOptions.Baggage | ActivityTrackingOptions.Tags; });
            logging.AddFilter("Microsoft.AspNetCore.*", LogLevel.Warning);
            logging.AddConsole();

            var resourceBuilder = Telemetry.GetResourceBuilder(ref serviceName, ref serviceVersion, ref otlpEndpoint);

            logging.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.IncludeFormattedMessage = true;
                options.ParseStateValues = true;
                // options.AttachLogsToActivityEvent();

                options.SetResourceBuilder(resourceBuilder);
                options.AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri(otlpEndpoint);
                    o.Protocol = OtlpExportProtocol.Grpc;
                });

                // options.AddConsoleExporter();
            });
        });

        return services;
    }
}
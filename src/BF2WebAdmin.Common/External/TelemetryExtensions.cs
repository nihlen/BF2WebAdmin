// ReSharper disable All

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace BF2WebAdmin.Common.External;

// https://docs.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs
public static class TelemetryExtensions
{
    /// <summary>
    /// How often the metrics are exported. The game servers' health windows come every 10 s, and their gauges show the
    /// latest window, so a longer interval would skip windows.
    /// </summary>
    public const int MetricExportIntervalMilliseconds = 10_000;

    /// <summary>
    /// Setup OpenTelemetry tracing and metrics to the specified endpoint for ASP.NET Core projects.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="serviceName">Service name (ex. bf2-webadmin)</param>
    /// <param name="serviceVersion">Service version (ex. 1.0.0)</param>
    /// <param name="otlpEndpoint">The OpenTelemetry protocol endpoint URL. Default is http://localhost:4317 with gRPC protocol.</param>
    /// <returns></returns>
    public static IServiceCollection? AddCustomTelemetry(this IServiceCollection? services, string? serviceName = null, string? serviceVersion = null, string? otlpEndpoint = null)
    {
        if (services is null)
            return null;

        // Fills in the defaults (and names the activity source) before anything below reads them
        var resourceBuilder = Telemetry.GetResourceBuilder(ref serviceName, ref serviceVersion, ref otlpEndpoint);
        var endpoint = new Uri(otlpEndpoint!);

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName!, serviceVersion: serviceVersion))
            .WithTracing(b => b
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = endpoint;
                    o.Protocol = OtlpExportProtocol.Grpc;
                })
                .AddAspNetCoreInstrumentation(o => { o.RecordException = true; })
                .AddHttpClientInstrumentation(o => { o.RecordException = true; })
                .AddEntityFrameworkCoreInstrumentation())
            .WithMetrics(b => b
                .AddMeter(Telemetry.MeterName)
                .AddOtlpExporter((o, reader) =>
                {
                    o.Endpoint = endpoint;
                    o.Protocol = OtlpExportProtocol.Grpc;
                    reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = MetricExportIntervalMilliseconds;
                })
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
            );

        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.Configure(options => { options.ActivityTrackingOptions = ActivityTrackingOptions.SpanId | ActivityTrackingOptions.TraceId | ActivityTrackingOptions.ParentId | ActivityTrackingOptions.Baggage | ActivityTrackingOptions.Tags; });
            logging.AddFilter("Microsoft.AspNetCore.*", LogLevel.Warning);
            logging.AddConsole();

            logging.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.IncludeFormattedMessage = true;
                options.ParseStateValues = true;

                options.SetResourceBuilder(resourceBuilder);
                options.AddOtlpExporter(o =>
                {
                    o.Endpoint = endpoint;
                    o.Protocol = OtlpExportProtocol.Grpc;
                });
            });
        });

        return services;
    }
}

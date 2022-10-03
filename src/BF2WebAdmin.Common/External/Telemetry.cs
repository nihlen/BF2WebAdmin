// ReSharper disable All
using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Nihlen.Common.Telemetry;

public static class Telemetry
{
    public static ActivitySource ActivitySource = new("Unknown Application", "1.0.0");
    public static Meter Meter = new("Unknown Application", "1.0.0");

    /// <summary>
    /// Create a TracerProvider for console applications. using var tracerProvider = Telemetry.CreateTracerProvider("ServiceName", ...);
    /// </summary>
    /// <param name="serviceName">Service name (ex. Nihlen.Common)</param>
    /// <param name="serviceVersion">Service version (ex. 1.0.0)</param>
    /// <param name="otlpEndpoint">The OpenTelemetry protocol endpoint URL. Default is http://localhost:4317 with gRPC protocol.</param>
    /// <returns>A configured TracerProvider</returns>
    public static TracerProvider CreateTracerProvider(string? serviceName = null, string? serviceVersion = null, string? otlpEndpoint = null)
    {
        var resourceBuilder = GetResourceBuilder(ref serviceName, ref serviceVersion, ref otlpEndpoint);

        return Sdk.CreateTracerProviderBuilder()
            .AddSource(serviceName)
            .SetResourceBuilder(resourceBuilder)
            .AddOtlpExporter(o =>
            {
                o.Endpoint = new Uri(otlpEndpoint);
                o.Protocol = OtlpExportProtocol.Grpc;
            })
            .AddHttpClientInstrumentation(o =>
            {
                o.RecordException = true;
            })
            .AddSqlClientInstrumentation(o =>
            {
                o.SetDbStatementForText = true;
                o.SetDbStatementForStoredProcedure = true;
                o.EnableConnectionLevelAttributes = true;
                o.RecordException = true;
            })
            .Build();
    }

    /// <summary>
    /// Create a MeterProvider for console applications. using var meterProvider = Telemetry.CreateMeterProvider("ServiceName", ...);
    /// </summary>
    /// <param name="serviceName">Service name (ex. Nihlen.Common)</param>
    /// <param name="serviceVersion">Service version (ex. 1.0.0)</param>
    /// <param name="otlpEndpoint">The OpenTelemetry protocol endpoint URL. Default is http://localhost:4317 with gRPC protocol.</param>
    /// <returns>A configured MeterProvider</returns>
    public static MeterProvider CreateMeterProvider(string? serviceName = null, string? serviceVersion = null, string? otlpEndpoint = null)
    {
        var resourceBuilder = GetResourceBuilder(ref serviceName, ref serviceVersion, ref otlpEndpoint);

        return Sdk.CreateMeterProviderBuilder()
            .AddMeter(serviceName)
            .SetResourceBuilder(resourceBuilder)
            .AddOtlpExporter(o =>
            {
                o.Endpoint = new Uri(otlpEndpoint);
                o.Protocol = OtlpExportProtocol.Grpc;
            })
            .AddHttpClientInstrumentation()
            .Build();
    }

    internal static ResourceBuilder GetResourceBuilder(ref string? serviceName, ref string? serviceVersion, ref string? otlpEndpoint)
    {
        var assemblyName = Assembly.GetExecutingAssembly().GetName();
        serviceName ??= assemblyName.FullName;
        serviceVersion ??= assemblyName.Version?.ToString() ?? "1.0.0";
        otlpEndpoint ??= "http://localhost:4317";

        ActivitySource = new ActivitySource(serviceName, serviceVersion);
        Meter = new Meter(serviceName, serviceVersion);

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: serviceName,
                serviceVersion: serviceVersion
            );

        return resourceBuilder;
    }
}
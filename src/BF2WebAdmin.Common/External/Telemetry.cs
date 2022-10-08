// ReSharper disable All

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Runtime.CompilerServices;
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

    /// <summary>
    /// Start a new root activity. This span will not have any parent spans and will be shown correctly in Jaeger
    /// https://github.com/dotnet/runtime/issues/65528#issuecomment-1068855998
    /// </summary>
    /// <returns></returns>
    public static Activity StartRootActivity([CallerMemberName] string name = "", bool makeCurrent = true)
    {
        var previous = Activity.Current;

        // by default StartActivity parents to the current span, we don't want it to have a parent
        Activity.Current = null;
        var newRoot = ActivitySource.StartActivity(name);

        // by default StartActivity() makes the new span current, but if wanted to keep the old span as current then we need this...
        if (makeCurrent)
            Activity.Current = newRoot;
        else
            Activity.Current = previous;

        return newRoot;
    }
}

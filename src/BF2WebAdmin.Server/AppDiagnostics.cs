using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Nihlen.Common.Telemetry;

namespace BF2WebAdmin.Server;

// References:
// https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/semantic_conventions/README.md
// https://github.com/asynkron/protoactor-dotnet/blob/e792c805baf29a50af2a1bd665e2a21ded7662f3/src/Proto.Cluster/Metrics/ClusterMetrics.cs
// https://github.com/exceptionless/Exceptionless/blob/e8e73183d6e2ea4b5a9054ab2cb5c2e6932e7fbf/src/Exceptionless.Core/Utility/AppDiagnostics.cs
// https://github.com/apache/pulsar-dotpulsar/blob/be7c40e39bd5afc68aefa0460ab92ca7220eeefe/src/DotPulsar/Internal/DotPulsarMeter.cs
// https://github.com/piotrekmich79/metricsExample/blob/fc7ca2ff7a0e5ceac727a914f167d457151619ba/MetricsProvider/SharkRuntimeMeter.cs
// TODO: make non-static helper methods Track* and inject into modules/readers/writers?
public static class AppDiagnostics
{
    internal static readonly Counter<int> ConnectedClientsCounter = Telemetry.Meter.CreateCounter<int>("bf2wa.socket.clients.connected.count", description: "Socket server connections");
    internal static readonly Counter<int> AcceptedClientsCounter = Telemetry.Meter.CreateCounter<int>("bf2wa.socket.clients.accepted.count", description: "Accepted socket server connections");
    internal static readonly Counter<int> RejectedClientsCounter = Telemetry.Meter.CreateCounter<int>("bf2wa.socket.clients.rejected.count", description: "Rejected socket server connections");
    internal static readonly Counter<long> ReceivedMessagesCounter = Telemetry.Meter.CreateCounter<long>("bf2wa.socket.messages.received.count", description: "Received message lines from all connections");
    internal static readonly Counter<long> ErrorMessagesCounter = Telemetry.Meter.CreateCounter<long>("bf2wa.socket.messages.error.count", description: "Errors on received message lines from all connections");
    internal static readonly Counter<int> ServerConnectRetryCounter = Telemetry.Meter.CreateCounter<int>("bf2wa.socket.clients.retry.count", description: "Reconnect retry attempts to game servers");

    private static readonly Counter<int> MessageSendCounter = Telemetry.Meter.CreateCounter<int>("bf2wa.socket.message.send.count", description: "Messages sent");
    private static readonly Histogram<double> MessageSendDuration = Telemetry.Meter.CreateHistogram<double>("bf2wa.socket.message.send.duration", unit: "ms", description: "Message send duration");
    private static readonly Counter<int> MessageReceiveCounter = Telemetry.Meter.CreateCounter<int>("bf2wa.socket.message.receive.count", description: "Messages received");
    private static readonly Histogram<double> MessageReceiveDuration = Telemetry.Meter.CreateHistogram<double>("bf2wa.socket.message.receive.duration", unit: "ms", description: "Message receive duration");

    private static readonly Counter<int> EventHandleCounter = Telemetry.Meter.CreateCounter<int>("bf2wa.event.count", description: "Events handled");
    private static readonly Histogram<double> EventHandleDuration = Telemetry.Meter.CreateHistogram<double>("bf2wa.event.duration", unit: "ms", description: "Event handle duration");
    private static readonly Counter<int> CommandHandleCounter = Telemetry.Meter.CreateCounter<int>("bf2wa.command.count", description: "Commands handled");
    private static readonly Histogram<double> CommandHandleDuration = Telemetry.Meter.CreateHistogram<double>("bf2wa.command.duration", unit: "ms", description: "Command handle duration");

    static AppDiagnostics()
    {
        _ = Telemetry.Meter.CreateObservableGauge("bf2wa-gc.heap", () => GC.GetTotalMemory(false), "By", "GC Heap Size");
        _ = Telemetry.Meter.CreateObservableGauge("bf2wa-gen_0-gc.count", () => GC.CollectionCount(0), description: "Gen 0 GC Count");
        _ = Telemetry.Meter.CreateObservableGauge("bf2wa-gen_1-gc.count", () => GC.CollectionCount(1), description: "Gen 1 GC Count");
        _ = Telemetry.Meter.CreateObservableGauge("bf2wa-gen_2-gc.count", () => GC.CollectionCount(2), description: "Gen 2 GC Count");
        _ = Telemetry.Meter.CreateObservableCounter("bf2wa-alloc.rate", () => GC.GetTotalAllocatedBytes(), "By", "Allocation Rate");
        _ = Telemetry.Meter.CreateObservableCounter("bf2wa-gc.fragmentation", GetFragmentation, description: "GC Fragmentation");
        _ = Telemetry.Meter.CreateObservableGauge("bf2wa-monitor.lock.contention.count", () => Monitor.LockContentionCount, description: "Monitor Lock Contention Count");
        _ = Telemetry.Meter.CreateObservableCounter("bf2wa-threadpool.thread.count", () => ThreadPool.ThreadCount, description: "ThreadPool Thread Count");
        _ = Telemetry.Meter.CreateObservableGauge("bf2wa-threadpool.completed.items.count", () => ThreadPool.CompletedWorkItemCount, description: "ThreadPool Completed Work Item Count");
        _ = Telemetry.Meter.CreateObservableCounter("bf2wa-threadpool.queue.length", () => ThreadPool.PendingWorkItemCount, description: "ThreadPool Queue Length");
        _ = Telemetry.Meter.CreateObservableCounter("bf2wa-active.timer.count", () => Timer.ActiveCount, description: "Number of Active Timers");
        _ = Telemetry.Meter.CreateObservableCounter("process.cpu.time", GetProcessorTimes, "s", "Processor time of this process");

        // Not yet official: https://github.com/open-telemetry/opentelemetry-specification/pull/2392
        _ = Telemetry.Meter.CreateObservableGauge("process.cpu.count", () => Environment.ProcessorCount, description: "The number of available logical CPUs");
        _ = Telemetry.Meter.CreateObservableGauge("process.memory.usage", () => Process.GetCurrentProcess().WorkingSet64, "By", "The amount of physical memory in use");
        _ = Telemetry.Meter.CreateObservableGauge("process.memory.virtual", () => Process.GetCurrentProcess().VirtualMemorySize64, "By", "The amount of committed virtual memory");
    }

    private static double GetFragmentation()
    {
        var gcInfo = GC.GetGCMemoryInfo();
        return gcInfo.HeapSizeBytes != 0 ? gcInfo.FragmentedBytes * 100d / gcInfo.HeapSizeBytes : 0;
    }

    private static IEnumerable<Measurement<double>> GetProcessorTimes()
    {
        var process = Process.GetCurrentProcess();
        return new[]
        {
            new Measurement<double>(process.UserProcessorTime.TotalSeconds, new KeyValuePair<string, object?>("state", "user")),
            new Measurement<double>(process.PrivilegedProcessorTime.TotalSeconds, new KeyValuePair<string, object?>("state", "system")),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double GetMilliseconds(DateTimeOffset startTime) => (DateTimeOffset.UtcNow - startTime).TotalMilliseconds;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void TrackMessageReceive(DateTimeOffset startTime, string serverId)
    {
        var tagList = new TagList
        {
            { "serverid", serverId }
        };

        MessageReceiveCounter.Add(1, tagList);
        MessageReceiveDuration.Record(GetMilliseconds(startTime), tagList);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void TrackMessageSend(DateTimeOffset startTime, string serverId)
    {
        var tagList = new TagList
        {
            { "serverid", serverId }
        };

        MessageSendCounter.Add(1, tagList);
        MessageSendDuration.Record(GetMilliseconds(startTime), tagList);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void TrackEvent(string eventType, string moduleType, DateTimeOffset startTime)
    {
        var tagList = new TagList
        {
            { "eventtype", eventType },
            { "moduletype", moduleType }
        };

        EventHandleCounter.Add(1, tagList);
        EventHandleDuration.Record(GetMilliseconds(startTime), tagList);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void TrackCommand(string commandType, string moduleType, DateTimeOffset startTime)
    {
        var tagList = new TagList
        {
            { "commandtype", commandType },
            { "moduletype", moduleType }
        };

        CommandHandleCounter.Add(1, tagList);
        CommandHandleDuration.Record(GetMilliseconds(startTime), tagList);
    }
}

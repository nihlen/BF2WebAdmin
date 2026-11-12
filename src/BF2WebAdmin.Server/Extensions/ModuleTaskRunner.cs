using System.Diagnostics;
using BF2WebAdmin.Common;
using BF2WebAdmin.Common.External;

namespace BF2WebAdmin.Server.Extensions;

/// <summary>
/// The modules' <see cref="ITaskRunner"/>: a background task (a delayed round end, the jail guard) runs on the module
/// thread like the handlers, instead of next to them on the thread pool. Its awaits free the thread for events meanwhile.
/// </summary>
public sealed class ModuleTaskRunner(ModuleContext context, ILogger logger) : ITaskRunner
{
    public void RunBackgroundTask(string description, Func<Task> func, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return;

        _ = context.InvokeAsync(async () =>
        {
            using var activity = Telemetry.ActivitySource.StartActivity("BackgroundTask");
            activity?.SetTag("bf2wa.task-description", description);

            try
            {
                await func();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // The modules were reloaded or the server removed
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to complete background task: {Description}", description);
                activity?.SetStatus(ActivityStatusCode.Error, $"Background task failed: {ex.Message}");
            }
        });
    }
}

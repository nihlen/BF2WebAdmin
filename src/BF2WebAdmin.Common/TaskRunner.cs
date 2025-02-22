using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nihlen.Common.Telemetry;

namespace BF2WebAdmin.Common;

public interface ITaskRunner
{
    void RunBackgroundTask(string description, Func<Task> func, CancellationToken ct);
}

public class TaskRunner : ITaskRunner
{
    private readonly ILogger _logger;

    public TaskRunner(ILogger logger)
    {
        _logger = logger;
    }
    
    public void RunBackgroundTask(string description, Func<Task> func, CancellationToken ct)
    {
        _ = Task.Run(async () =>
        {
            using var activity = Telemetry.ActivitySource.StartActivity("BackgroundTask");
            activity?.SetTag("bf2wa.task-description", description);

            try
            {
                await func();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to complete background task: {Description}", description);
                activity?.SetStatus(ActivityStatusCode.Error, $"Background task failed: {ex.Message}");
            }
        }, ct);
    }
}

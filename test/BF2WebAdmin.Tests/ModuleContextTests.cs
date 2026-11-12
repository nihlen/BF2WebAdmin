using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BF2WebAdmin.Server;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Commands;
using BF2WebAdmin.Server.Communication;
using BF2WebAdmin.Server.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace BF2WebAdmin.Tests;

/// <summary>The module thread (<see cref="ModuleContext"/>) and the mediator on it.</summary>
public class ModuleContextTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task WorkFromManyThreads_RunsOneAtATime_AndResumesOnTheModuleThread()
    {
        using var context = new ModuleContext("test", NullLogger.Instance);
        var running = 0;
        var mostAtOnce = 0;
        var offThread = 0;
        var count = 0;

        async Task Work()
        {
            for (var i = 0; i < 5; i++)
            {
                var now = Interlocked.Increment(ref running);
                mostAtOnce = Math.Max(mostAtOnce, now);
                if (!context.IsCurrent)
                    Interlocked.Increment(ref offThread);
                count++; // Not interlocked: one at a time
                Thread.SpinWait(200);
                Interlocked.Decrement(ref running);
                await Task.Yield();
            }
        }

        var tasks = Enumerable.Range(0, 20).Select(_ => Task.Run(() => context.InvokeAsync(Work))).ToArray();
        await Task.WhenAll(tasks).WaitAsync(Timeout);

        Assert.Equal(1, mostAtOnce);
        Assert.Equal(0, offThread);
        Assert.Equal(100, count);
    }

    [Fact]
    public async Task InvokeAsync_FlowsTheCallersAsyncLocalAndItsErrors()
    {
        using var context = new ModuleContext("test", NullLogger.Instance);
        var local = new AsyncLocal<string>();
        local.Value = "web user";

        string? seen = null;
        await context.InvokeAsync(async () =>
        {
            await Task.Delay(10);
            seen = local.Value;
        }).WaitAsync(Timeout);
        Assert.Equal("web user", seen);

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.InvokeAsync(async () =>
        {
            await Task.Yield();
            throw new InvalidOperationException("test");
        }).WaitAsync(Timeout));
    }

    [Fact]
    public async Task AfterDispose_PendingWorkStillFinishes()
    {
        var context = new ModuleContext("test", NullLogger.Instance);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = context.InvokeAsync(async () => await release.Task);

        context.Dispose();
        release.SetResult();
        await finished.WaitAsync(Timeout);
        await context.InvokeAsync(() => Task.CompletedTask).WaitAsync(Timeout);
    }

    [Fact]
    public async Task Mediator_EventsAndCommandsOnTheModuleThread_AndAModulePublishingDoesNotWaitForItself()
    {
        using var context = new ModuleContext("test", NullLogger.Instance);
        var resolver = Substitute.For<IModuleResolver>();
        var mediator = new Mediator(resolver, NullLogger<Mediator>.Instance, context);
        var threads = new List<bool>();

        // A RoundEndEvent handler that publishes a MatchEndEvent, like Heli2v2
        resolver.EventHandlers.Returns(new Dictionary<Type, IList<Func<IEvent, ValueTask>>>
        {
            [typeof(GameStateChangedEvent)] = new List<Func<IEvent, ValueTask>>
            {
                async _ =>
                {
                    threads.Add(context.IsCurrent);
                    await mediator.PublishAsync(new MapsChangedEvent([], DateTimeOffset.UtcNow));
                },
            },
            [typeof(MapsChangedEvent)] = new List<Func<IEvent, ValueTask>> { _ => { threads.Add(context.IsCurrent); return ValueTask.CompletedTask; } },
        });
        resolver.CommandHandlers.Returns(new Dictionary<Type, IList<Func<ICommand, ValueTask>>>
        {
            [typeof(TestCommand)] = new List<Func<ICommand, ValueTask>> { _ => { threads.Add(context.IsCurrent); return ValueTask.CompletedTask; } },
        });

        await mediator.PublishAsync(new GameStateChangedEvent(GameState.Playing, DateTimeOffset.UtcNow)).AsTask().WaitAsync(Timeout);
        await mediator.HandleAsync(new TestCommand()).WaitAsync(Timeout);

        Assert.Equal([true, true, true], threads);
    }

    private sealed class TestCommand : BaseCommand;
}

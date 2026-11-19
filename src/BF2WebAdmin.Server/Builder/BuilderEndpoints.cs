using System.Security.Claims;
using BF2WebAdmin.Server.AdminChannel;
using BF2WebAdmin.Server.Entities;
using BF2WebAdmin.Server.Modules;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BF2WebAdmin.Server.Builder;

/// <summary>The web map builder's API under /api/servers/{serverId}/builder (docs/map-builder.md).</summary>
public static class BuilderEndpoints
{
    public static void MapBuilderEndpoints(this RouteGroupBuilder servers)
    {
        var builder = servers.MapGroup("/{serverId}/builder").WithTags("Map builder");
        builder.MapGet("/", GetState);
        builder.MapPost("/apply", ApplyAsync);
        builder.MapPost("/clear", ClearAsync);
        builder.MapPost("/templates", FilterTemplatesAsync);
        builder.MapGet("/layouts", GetLayoutsAsync);
        builder.MapPost("/layouts", SaveLayoutAsync);
        builder.MapPost("/layouts/{layoutId:guid}/load", LoadLayoutAsync);
        builder.MapDelete("/layouts/{layoutId:guid}", DeleteLayoutAsync);
        builder.MapPost("/layouts/{layoutId:guid}/vote", StartVoteAsync);
    }

    private static MapBuilder? Find(string serverId, IGameServerRegistry registry) =>
        (registry.GetGameServer(serverId) as GameServer)?.Builder;

    private static string User(ClaimsPrincipal user) => user.Identity?.Name ?? "web";

    private static Results<Ok<BuilderStateDto>, NotFound> GetState(string serverId, IGameServerRegistry registry) =>
        Find(serverId, registry) is { } builder ? TypedResults.Ok(builder.GetState()) : TypedResults.NotFound();

    /// <summary>Applies the drafts in one go. 409 when the builder changed since the revision they were made against.</summary>
    private static Task<Results<Ok<BuilderApplyResult>, NotFound, Conflict<ProblemDetails>, BadRequest<ProblemDetails>>> ApplyAsync(
        string serverId, BuilderApplyRequest request, IGameServerRegistry registry, ClaimsPrincipal user) =>
        Run(serverId, registry, b => b.ApplyAsync(request, User(user)));

    /// <summary>Removes everything built; returns whether the round was restarted to do it (a module that cannot resync).</summary>
    private static Task<Results<Ok<bool>, NotFound, Conflict<ProblemDetails>, BadRequest<ProblemDetails>>> ClearAsync(
        string serverId, IGameServerRegistry registry, ClaimsPrincipal user) =>
        Run(serverId, registry, b => b.ClearAsync(User(user)));

    /// <summary>
    /// Those of the object templates (the web builder's catalog) that the running level uses, the only ones that can be
    /// built. The first call on a map asks the game server about each; the answers are kept until the map changes.
    /// </summary>
    private static Task<Results<Ok<IReadOnlyList<string>>, NotFound, Conflict<ProblemDetails>, BadRequest<ProblemDetails>>> FilterTemplatesAsync(
        string serverId, IReadOnlyList<string> templates, IGameServerRegistry registry, CancellationToken cancellationToken) =>
        Run(serverId, registry, b => b.FilterTemplatesAsync(templates, cancellationToken));

    private static Task<Results<Ok<IReadOnlyList<LayoutDto>>, NotFound, Conflict<ProblemDetails>, BadRequest<ProblemDetails>>> GetLayoutsAsync(
        string serverId, IGameServerRegistry registry) =>
        Run(serverId, registry, b => b.GetLayoutsAsync());

    /// <summary>Saves what is built as a layout of the running map. 409 when the name exists and overwrite is false.</summary>
    private static Task<Results<Ok<LayoutDto>, NotFound, Conflict<ProblemDetails>, BadRequest<ProblemDetails>>> SaveLayoutAsync(
        string serverId, SaveLayoutRequest request, IGameServerRegistry registry, ClaimsPrincipal user) =>
        Run(serverId, registry, b => b.SaveLayoutAsync(request.Name, request.Overwrite, User(user)));

    /// <summary>Replaces what is built with the layout.</summary>
    private static Task<Results<Ok<BuilderApplyResult>, NotFound, Conflict<ProblemDetails>, BadRequest<ProblemDetails>>> LoadLayoutAsync(
        string serverId, Guid layoutId, IGameServerRegistry registry, ClaimsPrincipal user) =>
        Run(serverId, registry, b => b.LoadLayoutAsync(layoutId, User(user)));

    /// <summary>
    /// Starts a vote among the players on loading the layout (<see cref="LayoutVoteModule"/>), for a user of another site
    /// (BF2v2's layout list): <see cref="StartLayoutVoteRequest.StarterId"/> is that user's id, for the cooldown of two
    /// minutes between their votes. 204 when it started, 409 with the reason when not (a vote runs, the cooldown, another
    /// map, nobody online, a module without client votes, a server without the map builder).
    /// </summary>
    private static async Task<Results<NoContent, NotFound, Conflict<ProblemDetails>, BadRequest<ProblemDetails>>> StartVoteAsync(
        string serverId, Guid layoutId, StartLayoutVoteRequest request, IGameServerRegistry registry)
    {
        if (string.IsNullOrWhiteSpace(request.StarterId) || string.IsNullOrWhiteSpace(request.StarterName))
            return TypedResults.BadRequest(new ProblemDetails { Title = "A starter id and name are needed.", Status = 400 });
        if (registry.GetGameServer(serverId) is not GameServer server)
            return TypedResults.NotFound();
        if (server.ModManager?.GetModule<LayoutVoteModule>() == null)
            return TypedResults.Conflict(new ProblemDetails { Title = "This server has no layout votes (its map builder is off).", Status = 409 });

        var result = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await server.PublishAsync(new LayoutVoteRequestEvent(layoutId, "web:" + request.StarterId.Trim(),
            AdminGameWriter.FitUtf8(request.StarterName.Trim(), 32), result, DateTimeOffset.UtcNow));
        var error = await result.Task.WaitAsync(TimeSpan.FromSeconds(15));
        return error == null
            ? TypedResults.NoContent()
            : TypedResults.Conflict(new ProblemDetails { Title = error, Status = 409 });
    }

    private static async Task<Results<NoContent, NotFound>> DeleteLayoutAsync(string serverId, Guid layoutId, IGameServerRegistry registry)
    {
        if (Find(serverId, registry) is not { } builder)
            return TypedResults.NotFound();

        await builder.DeleteLayoutAsync(layoutId);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<T>, NotFound, Conflict<ProblemDetails>, BadRequest<ProblemDetails>>> Run<T>(
        string serverId, IGameServerRegistry registry, Func<MapBuilder, Task<T>> action)
    {
        if (Find(serverId, registry) is not { } builder)
            return TypedResults.NotFound();

        try
        {
            return TypedResults.Ok(await action(builder));
        }
        catch (BuilderException e) when (e.NotFound)
        {
            return TypedResults.NotFound();
        }
        catch (BuilderException e)
        {
            var problem = new ProblemDetails { Title = e.Message, Status = e.Conflict ? 409 : 400 };
            return e.Conflict ? TypedResults.Conflict(problem) : TypedResults.BadRequest(problem);
        }
    }
}

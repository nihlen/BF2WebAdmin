using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BF2WebAdmin.Server.Builder;

/// <summary>
/// The saved layouts for other sites under /api/layouts, with the admin API key (docs/map-builder.md, "Layout API"):
/// BF2v2's offline editor lists and loads any layout, saves new ones for its users and updates a layout only for the user
/// who made it (<see cref="LayoutService"/>). Starting a vote on one is the builder's
/// <c>POST /api/servers/{id}/builder/layouts/{layoutId}/vote</c>.
/// </summary>
public static class LayoutEndpoints
{
    public static void MapLayoutEndpoints(this RouteGroupBuilder layouts)
    {
        layouts.MapGet("/", ListAsync);
        layouts.MapGet("/{layoutId:guid}", GetAsync);
        layouts.MapPost("/", CreateAsync);
        layouts.MapPut("/{layoutId:guid}", UpdateAsync);
    }

    /// <summary>The layouts of a map, or of every map without one, by map and name, without their objects.</summary>
    private static Task<Results<Ok<IReadOnlyList<LayoutSummaryDto>>, NotFound, Conflict<ProblemDetails>, BadRequest<ProblemDetails>, ProblemHttpResult>> ListAsync(
        LayoutService layouts, string? map = null) =>
        Run(() => layouts.ListAsync(map), TypedResults.Ok);

    private static Task<Results<Ok<LayoutDetailDto>, NotFound, Conflict<ProblemDetails>, BadRequest<ProblemDetails>, ProblemHttpResult>> GetAsync(
        Guid layoutId, LayoutService layouts) =>
        Run(() => layouts.GetAsync(layoutId), TypedResults.Ok);

    /// <summary>A new layout for a user of the other site. 409 when the map has a layout of that name, or the user has 50.</summary>
    private static Task<Results<Created<LayoutDetailDto>, NotFound, Conflict<ProblemDetails>, BadRequest<ProblemDetails>, ProblemHttpResult>> CreateAsync(
        CreateLayoutRequest request, LayoutService layouts) =>
        Run(() => layouts.CreateAsync(request), l => TypedResults.Created($"/api/layouts/{l.Id}", l));

    /// <summary>Replaces a layout's objects (and its name, when given). 403 unless the request's owner made it.</summary>
    private static Task<Results<Ok<LayoutDetailDto>, NotFound, Conflict<ProblemDetails>, BadRequest<ProblemDetails>, ProblemHttpResult>> UpdateAsync(
        Guid layoutId, UpdateLayoutRequest request, LayoutService layouts) =>
        Run(() => layouts.UpdateAsync(layoutId, request), TypedResults.Ok);

    private static async Task<Results<TOk, NotFound, Conflict<ProblemDetails>, BadRequest<ProblemDetails>, ProblemHttpResult>> Run<T, TOk>(
        Func<Task<T>> action, Func<T, TOk> ok) where TOk : IResult
    {
        try
        {
            return ok(await action());
        }
        catch (BuilderException e) when (e.NotFound)
        {
            return TypedResults.NotFound();
        }
        catch (BuilderException e) when (e.Forbidden)
        {
            return TypedResults.Problem(title: e.Message, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (BuilderException e)
        {
            var problem = new ProblemDetails { Title = e.Message, Status = e.Conflict ? 409 : 400 };
            return e.Conflict ? TypedResults.Conflict(problem) : TypedResults.BadRequest(problem);
        }
    }
}

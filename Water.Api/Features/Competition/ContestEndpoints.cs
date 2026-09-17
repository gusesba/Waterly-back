using System.Security.Claims;
using Water.Application.Competition;

namespace Water.Api.Features.Competition;

public static class ContestEndpoints
{
    public static IEndpointRouteBuilder MapContestEndpoints(this IEndpointRouteBuilder endpoints, bool requireRateLimiting = true)
    {
        var group = endpoints.MapGroup("/api/v1").RequireAuthorization().WithTags("Contests");
        if (requireRateLimiting) group.RequireRateLimiting("social");
        group.MapGet("/contests", GetAllAsync);
        group.MapGet("/contests/{contestId:guid}", GetAsync);
        group.MapPost("/contests/{contestId:guid}/join", JoinAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAllAsync(ClaimsPrincipal principal, IContestService service, CancellationToken token) =>
        await Result(() => service.GetAllAsync(UserId(principal), token));
    private static async Task<IResult> GetAsync(Guid contestId, ClaimsPrincipal principal, IContestService service, CancellationToken token) =>
        await Result(() => service.GetAsync(UserId(principal), contestId, token));
    private static async Task<IResult> JoinAsync(Guid contestId, JoinContestRequest request, ClaimsPrincipal principal, IContestService service, CancellationToken token) =>
        await Result(() => service.JoinAsync(UserId(principal), contestId, request.ClientOperationId, token));

    private static async Task<IResult> Result<T>(Func<Task<T>> action)
    {
        try { return TypedResults.Ok(await action()); }
        catch (ContestNotFoundException) { return TypedResults.NotFound(); }
        catch (ContestConflictException) { return TypedResults.Conflict(); }
        catch (ContestValidationException) { return TypedResults.BadRequest(); }
    }

    private static string UserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated user has no identifier.");
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Water.Application.Competition;

namespace Water.Api.Features.Competition;

public static class ContestEndpoints
{
    public const string AdminPolicy = "contest-admin";

    public static IEndpointRouteBuilder MapContestEndpoints(this IEndpointRouteBuilder endpoints, bool requireRateLimiting = true)
    {
        var reads = endpoints.MapGroup("/api/v1").RequireAuthorization().WithTags("Contests");
        var writes = endpoints.MapGroup("/api/v1").RequireAuthorization().WithTags("Contests");
        if (requireRateLimiting)
        {
            reads.RequireRateLimiting("contest-read");
            writes.RequireRateLimiting("contest-write");
        }
        reads.MapGet("/contests", GetAllAsync);
        reads.MapGet("/contests/capabilities", GetCapabilitiesAsync);
        reads.MapGet("/contests/{contestId:guid}", GetAsync);
        reads.MapGet("/contests/{contestId:guid}/score", GetScoreAsync);
        reads.MapGet("/contests/{contestId:guid}/leaderboard", GetLeaderboardAsync);
        reads.MapGet("/medals", GetMedalsAsync);
        writes.MapPost("/contests/{contestId:guid}/join", JoinAsync);
        writes.MapPost("/admin/contests", CreateAsync).RequireAuthorization(AdminPolicy);
        return endpoints;
    }

    private static async Task<IResult> GetAllAsync(ClaimsPrincipal principal, IContestService service, CancellationToken token) =>
        await Result(() => service.GetAllAsync(UserId(principal), token));
    private static async Task<IResult> GetCapabilitiesAsync(ClaimsPrincipal principal, IAuthorizationService authorizationService) =>
        TypedResults.Ok(new ContestCapabilitiesResponse((await authorizationService.AuthorizeAsync(principal, AdminPolicy)).Succeeded));
    private static async Task<IResult> GetAsync(Guid contestId, ClaimsPrincipal principal, IContestService service, CancellationToken token) =>
        await Result(() => service.GetAsync(UserId(principal), contestId, token));
    private static async Task<IResult> GetScoreAsync(Guid contestId, ClaimsPrincipal principal, IContestScoreService service, CancellationToken token)
    {
        try { return TypedResults.Ok(await service.GetAsync(UserId(principal), contestId, token)); }
        catch (ContestNotFoundException) { return TypedResults.NotFound(); }
        catch (Water.Application.Hydration.HydrationProfileRequiredException) { return TypedResults.Conflict(); }
    }
    private static async Task<IResult> GetLeaderboardAsync(
        Guid contestId,
        int page,
        int pageSize,
        ClaimsPrincipal principal,
        IContestLeaderboardService service,
        CancellationToken token)
    {
        try { return TypedResults.Ok(await service.GetAsync(UserId(principal), contestId, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize, token)); }
        catch (ContestNotFoundException) { return TypedResults.NotFound(); }
        catch (ContestValidationException) { return TypedResults.BadRequest(); }
        catch (Water.Application.Hydration.HydrationProfileRequiredException) { return TypedResults.Conflict(); }
    }
    private static async Task<IResult> GetMedalsAsync(ClaimsPrincipal principal, IMedalService service, CancellationToken token) =>
        TypedResults.Ok(await service.GetAsync(UserId(principal), token));
    private static async Task<IResult> JoinAsync(Guid contestId, JoinContestRequest request, ClaimsPrincipal principal, IContestService service, CancellationToken token) =>
        await Result(() => service.JoinAsync(UserId(principal), contestId, request.ClientOperationId, token));
    private static async Task<IResult> CreateAsync(CreateContestRequest request, ClaimsPrincipal principal, IContestService service, CancellationToken token)
    {
        try
        {
            var contest = await service.CreateAsync(UserId(principal), request, token);
            return TypedResults.Created($"/api/v1/contests/{contest.Id}", contest);
        }
        catch (ContestValidationException) { return TypedResults.BadRequest(); }
    }

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

using System.Security.Claims;
using Water.Application.Hydration;

namespace Water.Api.Features.Hydration;

public static class HydrationEndpoints
{
    public static IEndpointRouteBuilder MapHydrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/hydration")
            .RequireAuthorization()
            .WithTags("Hydration");

        group.MapGet("/today", GetTodayAsync);
        group.MapGet("/history", GetHistoryAsync);
        group.MapGet("/beverages", GetBeveragesAsync);
        group.MapGet("/suggestions", GetSuggestionsAsync);
        group.MapPost("/entries", AddEntryAsync);
        group.MapPatch("/entries/{entryId:guid}", UpdateEntryAsync);
        group.MapDelete("/entries/{entryId:guid}", DeleteEntryAsync);

        return endpoints;
    }

    private static async Task<IResult> GetTodayAsync(
        ClaimsPrincipal principal,
        IHydrationService hydrationService,
        CancellationToken cancellationToken)
    {
        try
        {
            return TypedResults.Ok(await hydrationService.GetTodayAsync(
                GetUserId(principal),
                cancellationToken));
        }
        catch (HydrationProfileRequiredException)
        {
            return TypedResults.Conflict();
        }
    }

    private static async Task<IResult> AddEntryAsync(
        AddDrinkEntryRequest request,
        ClaimsPrincipal principal,
        IHydrationService hydrationService,
        CancellationToken cancellationToken)
    {
        try
        {
            return TypedResults.Ok(await hydrationService.AddEntryAsync(
                GetUserId(principal),
                request,
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [exception.ParamName ?? "request"] = [exception.Message]
            });
        }
        catch (HydrationProfileRequiredException)
        {
            return TypedResults.Conflict();
        }
        catch (IdempotencyConflictException)
        {
            return TypedResults.Conflict();
        }
        catch (BeverageNotFoundException)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["beverageCode"] = ["Beverage is invalid."]
            });
        }
    }

    private static async Task<IResult> UpdateEntryAsync(
        Guid entryId,
        UpdateDrinkEntryRequest request,
        ClaimsPrincipal principal,
        IHydrationService hydrationService,
        CancellationToken cancellationToken)
    {
        try
        {
            return TypedResults.Ok(await hydrationService.UpdateEntryAsync(
                GetUserId(principal), entryId, request, cancellationToken));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [exception.ParamName ?? "volumeMl"] = [exception.Message]
            });
        }
        catch (DrinkEntryNotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (BeverageNotFoundException)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["beverageCode"] = ["Beverage is invalid."]
            });
        }
        catch (IdempotencyConflictException)
        {
            return TypedResults.Conflict();
        }
        catch (ArgumentException exception)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [exception.ParamName ?? "request"] = [exception.Message]
            });
        }
    }

    private static async Task<IResult> DeleteEntryAsync(
        Guid entryId,
        Guid? clientOperationId,
        ClaimsPrincipal principal,
        IHydrationService hydrationService,
        CancellationToken cancellationToken)
    {
        try
        {
            return TypedResults.Ok(await hydrationService.DeleteEntryAsync(
                GetUserId(principal), entryId, clientOperationId ?? Guid.Empty, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [exception.ParamName ?? "clientOperationId"] = [exception.Message]
            });
        }
        catch (DrinkEntryNotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (IdempotencyConflictException)
        {
            return TypedResults.Conflict();
        }
    }

    private static async Task<IResult> GetHistoryAsync(
        int? days,
        ClaimsPrincipal principal,
        IHydrationService hydrationService,
        CancellationToken cancellationToken)
    {
        try
        {
            return TypedResults.Ok(await hydrationService.GetHistoryAsync(
                GetUserId(principal), days ?? 7, cancellationToken));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [exception.ParamName ?? "days"] = [exception.Message]
            });
        }
        catch (HydrationProfileRequiredException)
        {
            return TypedResults.Conflict();
        }
    }

    private static async Task<IResult> GetBeveragesAsync(
        ClaimsPrincipal principal,
        IHydrationService hydrationService,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await hydrationService.GetBeveragesAsync(
            GetUserId(principal), cancellationToken));
    }

    private static async Task<IResult> GetSuggestionsAsync(
        string? beverageCode,
        ClaimsPrincipal principal,
        IHydrationService hydrationService,
        CancellationToken cancellationToken)
    {
        return TypedResults.Ok(await hydrationService.GetSuggestionsAsync(
            GetUserId(principal), beverageCode ?? "water", cancellationToken));
    }

    private static string GetUserId(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user has no identifier.");
    }
}

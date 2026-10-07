using Microsoft.EntityFrameworkCore;
using Water.Application.Profiles;
using Water.Domain.Hydration;
using Water.Domain.Profiles;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Profiles;

public sealed class ProfileService(WaterDbContext dbContext, TimeProvider timeProvider) : IProfileService
{
    private static readonly HashSet<string> AllowedGoals =
    [
        "habit",
        "energy",
        "training",
        "focus",
        "wellbeing",
        "soda"
    ];

    public async Task<CurrentUserResponse> GetCurrentUserAsync(
        string userId,
        string email,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.Profiles
            .Include(item => item.Goals)
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), ResolveTimeZone(profile?.TimeZone ?? "UTC")).Date);
        var hydrationGoal = await dbContext.HydrationGoals
            .Where(item => item.UserId == userId && item.EffectiveFrom <= today)
            .OrderByDescending(item => item.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        return Map(email, profile, hydrationGoal);
    }

    public async Task<CurrentUserResponse> CompleteOnboardingAsync(
        string userId,
        string email,
        CompleteOnboardingRequest request,
        CancellationToken cancellationToken)
    {
        var timeZone = ResolveTimeZone(request.TimeZone);
        var goals = request.Goals
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (goals.Any(goal => !AllowedGoals.Contains(goal)))
        {
            throw new ArgumentException(
                "One or more goals are invalid.",
                nameof(CompleteOnboardingRequest.Goals));
        }

        var profile = await dbContext.Profiles
            .Include(item => item.Goals)
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (profile is null)
        {
            profile = new UserProfile(
                userId,
                request.Age,
                request.HeightCm,
                request.WeightKg,
                request.TimeZone,
                goals);
            dbContext.Profiles.Add(profile);
        }
        else
        {
            profile.Update(request.Age, request.HeightCm, request.WeightKg, request.TimeZone, goals);
        }

        var effectiveFrom = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone).Date);
        var hydrationGoal = await dbContext.HydrationGoals
            .Where(item => item.UserId == userId && item.EffectiveFrom <= effectiveFrom)
            .OrderByDescending(item => item.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (hydrationGoal is null)
        {
            hydrationGoal = new HydrationGoal(userId, request.DailyTargetMl, effectiveFrom);
            dbContext.HydrationGoals.Add(hydrationGoal);
        }
        else if (hydrationGoal.DailyTargetMl != request.DailyTargetMl)
        {
            // Repeating onboarding must follow the same frozen-day policy as goal settings.
            var tomorrow = effectiveFrom.AddDays(1);
            var scheduled = await dbContext.HydrationGoals.SingleOrDefaultAsync(
                item => item.UserId == userId && item.EffectiveFrom == tomorrow, cancellationToken);
            if (scheduled is null)
                dbContext.HydrationGoals.Add(new HydrationGoal(userId, request.DailyTargetMl, tomorrow));
            else
                scheduled.UpdateTarget(request.DailyTargetMl);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(email, profile, hydrationGoal);
    }

    private static CurrentUserResponse Map(
        string email,
        UserProfile? profile,
        HydrationGoal? hydrationGoal)
    {
        return new CurrentUserResponse(
            email,
            profile is not null && hydrationGoal is not null,
            profile is null
                ? null
                : new ProfileResponse(
                    profile.Age,
                    profile.HeightCm,
                    profile.WeightKg,
                    profile.Goals.Select(item => item.Goal).Order().ToArray()),
            hydrationGoal is null
                ? null
                : new HydrationGoalResponse(
                    hydrationGoal.DailyTargetMl,
                    hydrationGoal.EffectiveFrom,
                    profile?.TimeZone ?? "UTC"));
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new ArgumentException(
                "Time zone is invalid.",
                nameof(CompleteOnboardingRequest.TimeZone),
                exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new ArgumentException(
                "Time zone is invalid.",
                nameof(CompleteOnboardingRequest.TimeZone),
                exception);
        }
    }
}

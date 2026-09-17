using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Water.Application.Notifications;
using Water.Domain.Notifications;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Notifications;

public sealed class ContestNotificationOutbox(WaterDbContext dbContext) : IContestNotificationOutbox
{
    public async Task QueueResultAsync(string userId, Guid contestId, string contestName, int position, int drops, int prestige, int? medalPosition, int ruleVersion, DateTimeOffset createdAt, CancellationToken token)
    {
        var installations = await dbContext.DeviceInstallations
            .Where(item => item.UserId == userId && item.DisabledAt == null)
            .ToArrayAsync(token);
        foreach (var installation in installations)
        {
            var (title, body) = Copy(installation.Locale, contestName, position, drops, prestige, medalPosition);
            dbContext.PushNotificationMessages.Add(new PushNotificationMessage(
                installation.Id,
                "contest-result",
                contestId.ToString(),
                title,
                body,
                JsonSerializer.Serialize(new { kind = "contest-result", contestId }),
                $"contest:{contestId:N}:result:v{ruleVersion}",
                createdAt));
        }
    }

    private static (string Title, string Body) Copy(string locale, string contest, int position, int drops, int prestige, int? medalPosition)
    {
        if (locale == "pt-BR")
        {
            if (medalPosition is not null) return ("Resultado disponível", $"Você ficou em {position}º no concurso {contest} e ganhou {drops} Drops e {prestige} Prestige.");
            if (drops > 0 || prestige > 0) return ("Resultado disponível", $"O resultado de {contest} chegou: +{drops} Drops e +{prestige} Prestige.");
            return ("Resultado disponível", $"O resultado final de {contest} está disponível.");
        }

        if (medalPosition is not null) return ("Result available", $"You placed {Ordinal(position)} in {contest} and earned {drops} Drops and {prestige} Prestige.");
        if (drops > 0 || prestige > 0) return ("Result available", $"The {contest} result is in: +{drops} Drops and +{prestige} Prestige.");
        return ("Result available", $"The final result for {contest} is available.");
    }

    private static string Ordinal(int position)
    {
        var suffix = position % 100 is 11 or 12 or 13 ? "th" : (position % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return $"{position}{suffix}";
    }
}

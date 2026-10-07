using Microsoft.AspNetCore.Identity;

namespace Water.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    // Legacy accounts stay null after migration; do not invent registration times.
    public DateTimeOffset? RegisteredAt { get; set; } = DateTimeOffset.UtcNow;
}

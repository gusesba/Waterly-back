using Water.Domain.Cosmetics;

namespace Water.Tests.Unit;

public sealed class CharacterLoadoutTests
{
    [Fact]
    public void New_loadout_uses_natural_aura_and_can_equip_another_aura()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var loadout = new CharacterLoadout("user-1", createdAt);

        Assert.Equal("natural", loadout.AuraCode);
        Assert.Equal(createdAt, loadout.UpdatedAt);

        var updatedAt = createdAt.AddMinutes(1);
        loadout.EquipAura("ocean", updatedAt);

        Assert.Equal("ocean", loadout.AuraCode);
        Assert.Equal(updatedAt, loadout.UpdatedAt);
    }
}

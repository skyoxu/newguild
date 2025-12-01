using System;
using Game.Core.Domain;
using Game.Core.Domain.ValueObjects;
using Game.Core.Services;
using Xunit;

namespace Game.Core.Tests.Services;

public class CombatServiceTests
{
    [Fact]
    public void CalculateDamage_applies_resistance_and_critical()
    {
        var cfg = new CombatConfig { CritMultiplier = 2.0 };
        cfg.Resistances[DamageType.Fire] = 0.5; // 50% resist

        var svc = new CombatService();
        var baseFire = new Damage(100, DamageType.Fire);
        var reduced = svc.CalculateDamage(baseFire, cfg);
        Assert.Equal(50, reduced);

        var crit = new Damage(100, DamageType.Fire, IsCritical: true);
        var reducedCrit = svc.CalculateDamage(crit, cfg);
        Assert.Equal(100, reducedCrit); // 100 * 0.5 * 2.0
    }

    [Fact]
    public void CalculateDamage_with_armor_mitigates_linearly()
    {
        var cfg = new CombatConfig();
        var svc = new CombatService();
        var dmg = new Damage(40, DamageType.Physical);
        var res = svc.CalculateDamage(dmg, cfg, armor: 10);
        Assert.Equal(30, res);
    }

    [Fact]
    public void ApplyDamage_reduces_player_health()
    {
        var p = new Player(maxHealth: 100);
        var svc = new CombatService();
        svc.ApplyDamage(p, new Damage(25, DamageType.Physical), playerId: "player-1");
        Assert.Equal(75, p.Health.Current);
    }

    [Fact]
    public void ApplyDamage_with_int_amount_reduces_player_health()
    {
        var p = new Player(maxHealth: 100);
        var svc = new CombatService();
        svc.ApplyDamage(p, amount: 30);
        Assert.Equal(70, p.Health.Current);
    }

    [Fact]
    public void ApplyDamage_with_config_applies_damage_correctly()
    {
        var p = new Player(maxHealth: 100);
        var cfg = new CombatConfig();
        cfg.Resistances[DamageType.Fire] = 0.5; // 50% resistance
        var svc = new CombatService();

        // 100 damage with 50% resistance = 50 damage
        svc.ApplyDamage(p, new Damage(100, DamageType.Fire), cfg, playerId: "player-1");
        Assert.Equal(50, p.Health.Current);
    }

    [Fact]
    public void CalculateDamage_with_default_config_uses_fallback()
    {
        var svc = new CombatService();
        var dmg = new Damage(50, DamageType.Physical);

        // Null config should use default
        var result = svc.CalculateDamage(dmg, config: null);
        Assert.Equal(50, result);
    }
}

using System.Collections.Generic;
using AetherialArena.Models;

public class Ability
{
    public int ID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ManaCost { get; set; }
    public TargetType Target { get; set; }
    public List<AbilityEffect> Effects { get; set; } = new();
}

public class AbilityEffect
{
    public EffectType EffectType { get; set; }
    public Stat StatAffected { get; set; } = Stat.None;
    public float Potency { get; set; }
    public int Duration { get; set; }
    public int InitialDelay { get; set; } = 0; // For DelayedStun and DelayedDamage
    public int Stacks { get; set; } = 1; // For StackingDebuff
    public TargetType? Target { get; set; }
}

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AetherialArena.Models
{
    public enum CombatLogColor
    {
        Normal,
        Damage,
        Heal,
        Status
    }

    public class CombatLogEntry
    {
        public string Message { get; set; }
        public CombatLogColor Color { get; set; }

        public CombatLogEntry(string message, CombatLogColor color = CombatLogColor.Normal)
        {
            Message = message;
            Color = color;
        }
    }

    public enum SpriteType
    {
        Figure,
        Beast,
        Creature,
        Mechanical
    }

    public enum RarityTier
    {
        Common,
        Uncommon,
        Rare,
        Boss
    }

    public enum SearchResult
    {
        Success,
        NoAether,
        InvalidState,
        NoSpritesFound
    }

    public class EncounterData
    {
        public ushort TerritoryTypeID { get; set; }

        [JsonPropertyName("Default")]
        public List<int>? Default { get; set; }

        [JsonPropertyName("EncountersBySubLocation")]
        public Dictionary<string, List<int>>? EncountersBySubLocation { get; set; }
    }


    public enum TargetType
    {
        Self,
        Enemy
    }

    public enum EffectType
    {
        Damage,
        Heal,
        StatBuff,
        StatDebuff,
        Stun,
        DamageOverTime,
        ManaDrain,
        LifeSteal,
        Reflect,
        DelayedStun,
        DelayedDamage,
        StackingDebuff,
        ChanceToStunOverTime,
        Casting,
        ConditionalDamage
    }

    public enum Stat
    {
        None,
        Attack,
        Defense,
        Speed
    }
}

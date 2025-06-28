using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AetherialArena.Models
{
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
        Rare
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

        [JsonPropertyName("SpriteIDs")]
        public List<int> DefaultSpriteIDs { set { EncountersBySubLocation["Default"] = value; } }

        [JsonPropertyName("EncountersBySubLocation")]
        public Dictionary<string, List<int>> EncountersBySubLocation { get; set; } = new();
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
        Stun
    }

    public enum Stat
    {
        None,
        Attack,
        Defense,
        Speed
    }
}

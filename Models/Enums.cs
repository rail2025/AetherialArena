using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AetherialArena.Models
{
    // Corrected to match the types used in your data (e.g., sprite.csv)
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
        InvalidState, // For being mounted, in combat, etc.
        NoSpritesFound
    }

    // A class to hold the data from encountertables.json

    public class EncounterData
    {
        public ushort TerritoryTypeID { get; set; }

        // This will correctly deserialize BOTH "SpriteIDs" and merge them into the main dictionary.
        [JsonPropertyName("SpriteIDs")]
        public List<int> DefaultSpriteIDs { set { EncountersBySubLocation["Default"] = value; } }

        [JsonPropertyName("EncountersBySubLocation")]
        public Dictionary<string, List<int>> EncountersBySubLocation { get; set; } = new();
    }
}

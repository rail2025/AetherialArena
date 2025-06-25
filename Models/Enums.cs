using System.Collections.Generic;

namespace AetherialArena.Models
{
    // Corrected to match the types used in your data (e.g., sprite.csv)
    public enum SpriteType
    {
        Figure,
        Beast,
        Creature,  // Changed from Wildlife
        Mechanical // Changed from Machina
    }

    public enum RarityTier
    {
        Common,
        Uncommon,
        Rare
    }

    // A class to hold the data from encountertables.json
    public class EncounterData
    {
        public ushort TerritoryTypeID { get; set; }
        public List<int> SpriteIDs { get; set; } = new();
    }
}

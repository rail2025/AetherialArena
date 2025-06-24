using System.Collections.Generic;

namespace AetherialArena.Models
{
    // We will use this later for type advantages
    public enum SpriteType
    {
        Figure,
        Beast,
        Wildlife,
        Machina
    }

    // A class to hold the data from encountertables.json
    public class EncounterData
    {
        public ushort TerritoryTypeID { get; set; }
        public List<int> SpriteIDs { get; set; } = new();
    }
}

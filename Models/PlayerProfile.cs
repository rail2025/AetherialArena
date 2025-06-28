using System.Collections.Generic;

namespace AetherialArena.Models
{
    public class PlayerProfile
    {
        public int CurrentAether { get; set; } = 10;
        public int MaxAether { get; set; } = 10;

        // The player now starts with sprites 1, 2, and 3 unlocked.
        public List<int> AttunedSpriteIDs { get; set; } = new() { 1, 2, 3 };

        public Dictionary<int, int> DefeatCounts { get; set; } = new();

        // Stores the IDs of the 3 sprites in the player's active loadout.
        public List<int> Loadout { get; set; } = new() { 1, 2, 3 };
    }
}

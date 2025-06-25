using System.Collections.Generic;

namespace AetherialArena.Models
{
    /// <summary>
    /// Stores all data specific to the player that needs to be saved.
    /// </summary>
    public class PlayerProfile
    {
        public int CurrentAether { get; set; } = 10;
        public int MaxAether { get; set; } = 10;

        // A list of IDs for all sprites the player has successfully captured.
        public List<int> AttunedSpriteIDs { get; set; } = new();

        // A dictionary to track progress towards capturing a sprite.
        // Key: Sprite ID, Value: number of times defeated.
        public Dictionary<int, int> DefeatCounts { get; set; } = new();
    }
}

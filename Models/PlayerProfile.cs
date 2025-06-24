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

        // We will add the list of attuned sprites here later.
        // public List<int> AttunedSpriteIDs { get; set; } = new();
    }
}

namespace AetherialArena.Models
{
    /// <summary>
    /// Defines a special action that a Sprite can perform in battle.
    /// </summary>
    public class Ability
    {
        // The unique identifier for this ability.
        public int ID { get; set; }

        // The name of the ability.
        public string Name { get; set; } = string.Empty;

        // The base power of the ability (e.g., damage dealt or health restored).
        public int Power { get; set; }

        // The amount of Mana required to use this ability.
        public int ManaCost { get; set; }
    }
}

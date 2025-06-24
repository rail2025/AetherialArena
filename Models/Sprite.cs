namespace AetherialArena.Models
{
    public class Sprite
    {
        public int ID { get; set; }
        public string Name { get; set; } = string.Empty;
        public SpriteType Type { get; set; }
        public string IconName { get; set; } = string.Empty; // Added IconName
        public int MaxHealth { get; set; }
        public int Health { get; set; }
        public int MaxMana { get; set; }
        public int Mana { get; set; }
        public int Speed { get; set; }
        public int Attack { get; set; }

        public Sprite() { }

        public Sprite(Sprite other)
        {
            ID = other.ID;
            Name = other.Name;
            Type = other.Type;
            IconName = other.IconName; // Added IconName
            MaxHealth = other.MaxHealth;
            Health = other.Health;
            MaxMana = other.MaxMana;
            Mana = other.Mana;
            Speed = other.Speed;
            Attack = other.Attack;
        }
    }
}

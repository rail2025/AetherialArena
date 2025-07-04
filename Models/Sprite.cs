using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AetherialArena.Models
{
    public class Sprite
    {
        public int ID { get; set; }
        public string Name { get; set; } = string.Empty;
        public RarityTier Rarity { get; set; }
        public SpriteType Type { get; set; }
        public string IconName { get; set; } = string.Empty;
        public string RecolorKey { get; set; } = "default";

        public int SpecialAbilityID { get; set; }

        // The old property is kept for loading the string from JSON, but ignored for other uses.
        public string SpecialAbility { get; set; } = string.Empty;

        public int MaxHealth { get; set; }
        public int MaxMana { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int Speed { get; set; }
        public string BaseHeal { get; set; } = string.Empty;
        public string SubType { get; set; } = string.Empty;
        public List<string> AttackType { get; set; } = new();
        public List<string> Weaknesses { get; set; } = new();
        public List<string> Resistances { get; set; } = new();
        public List<int> SubLocationIDs { get; set; } = new();

        [JsonIgnore]
        public int Health { get; set; }
        [JsonIgnore]
        public int Mana { get; set; }
        [JsonIgnore]
        public List<int> SpecialAbilityIDs { get; set; } = new();

        public Sprite() { }

        public Sprite(Sprite s)
        {
            this.ID = s.ID;
            this.Name = s.Name;
            this.Rarity = s.Rarity;
            this.Type = s.Type;
            this.IconName = s.IconName;
            this.RecolorKey = s.RecolorKey;
            this.MaxHealth = s.MaxHealth;
            this.MaxMana = s.MaxMana;
            this.Attack = s.Attack;
            this.Defense = s.Defense;
            this.Speed = s.Speed;
            this.SpecialAbilityID = s.SpecialAbilityID;
            this.SpecialAbility = s.SpecialAbility;
            this.BaseHeal = s.BaseHeal;
            this.SubLocationIDs = s.SubLocationIDs;
            this.Health = s.MaxHealth;
            this.Mana = s.MaxMana;
            this.SubType = s.SubType;
            this.AttackType = s.AttackType;
            this.Weaknesses = s.Weaknesses;
            this.Resistances = s.Resistances;
            this.SpecialAbilityIDs.Add(s.SpecialAbilityID);
        }
    }
}

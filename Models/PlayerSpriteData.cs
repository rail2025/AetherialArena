namespace AetherialArena.Models
{
    public class PlayerSpriteData
    {
        public int Level { get; set; } = 1;
        public int Experience { get; set; } = 0;
        public int UnspentStatPoints { get; set; } = 0;
        public int UnspentSkillPoints { get; set; } = 0;

        public int AllocatedHP { get; set; } = 0;
        public int AllocatedMP { get; set; } = 0;
        public int AllocatedAttack { get; set; } = 0;
        public int AllocatedDefense { get; set; } = 0;
        public int AllocatedSpeed { get; set; } = 0;

        public int? SecondSpecialAbilityID { get; set; } = null;
    }
}

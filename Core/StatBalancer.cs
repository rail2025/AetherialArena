using AetherialArena.Models;

namespace AetherialArena.Core
{
    
    public static class StatBalancer
    {
        private const int TotalPowerBudget = 100;
        private const double HealthPowerValue = 0.5;
        private const int ManaPowerValue = 1;
        private const int SpeedPowerValue = 2;
        private const int AttackPowerValue = 2;

        public static bool IsBalanced(Sprite sprite)
        {
            double calculatedPower = 0;
            calculatedPower += sprite.Health * HealthPowerValue;
            calculatedPower += sprite.Mana * ManaPowerValue;
            calculatedPower += sprite.Speed * SpeedPowerValue;
            calculatedPower += sprite.Attack * AttackPowerValue;

            return (int)calculatedPower == TotalPowerBudget;
        }
    }
}

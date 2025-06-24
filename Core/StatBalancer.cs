using AetherialArena.Models;

namespace AetherialArena.Core
{
    /// <summary>
    /// Contains the static logic for validating Sprite stats based on a budget.
    /// </summary>
    public static class StatBalancer
    {
        private const int TotalPowerBudget = 100;
        private const double HealthPowerValue = 0.5;
        private const int ManaPowerValue = 1;
        private const int SpeedPowerValue = 2;
        private const int AttackPowerValue = 2;

        /// <summary>
        /// Checks if a Sprite's stats adhere to the defined power budget.
        /// </summary>
        /// <param name="sprite">The Sprite to validate.</param>
        /// <returns>True if the stats are balanced, false otherwise.</returns>
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

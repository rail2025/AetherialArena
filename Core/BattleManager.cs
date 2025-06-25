using AetherialArena.Models;
using System;

namespace AetherialArena.Core
{
    public class BattleManager
    {
        public enum BattleState { None, InProgress, PlayerVictory, OpponentVictory }
        public enum BattleTurn { Player, Opponent }

        private readonly Plugin plugin;

        public Sprite? PlayerSprite { get; private set; }
        public Sprite? OpponentSprite { get; private set; }
        public BattleTurn CurrentTurn { get; private set; }
        public BattleState State { get; private set; } = BattleState.None;

        public BattleManager(Plugin p)
        {
            plugin = p;
        }

        public void StartBattle(Sprite playerSprite, Sprite opponentSprite)
        {
            PlayerSprite = playerSprite;
            OpponentSprite = opponentSprite;
            State = BattleState.InProgress;
            DetermineTurnOrder();
        }

        public void EndBattle()
        {
            State = BattleState.None;
            PlayerSprite = null;
            OpponentSprite = null;

            plugin.MainWindow.IsOpen = false;
            plugin.HubWindow.IsOpen = true;
        }

        public void Update()
        {
            if (CurrentTurn == BattleTurn.Opponent && State == BattleState.InProgress)
            {
                AITurn();
            }
        }

        public void PlayerAttack()
        {
            if (CurrentTurn != BattleTurn.Player || State != BattleState.InProgress) return;
            if (PlayerSprite == null || OpponentSprite == null) return;

            OpponentSprite.Health -= PlayerSprite.Attack;
            CheckForWinner();

            if (State == BattleState.InProgress)
            {
                CurrentTurn = BattleTurn.Opponent;
            }
        }

        private void AITurn()
        {
            if (PlayerSprite == null || OpponentSprite == null) return;

            PlayerSprite.Health -= OpponentSprite.Attack;
            CheckForWinner();

            if (State == BattleState.InProgress)
            {
                CurrentTurn = BattleTurn.Player;
            }
        }

        private void CheckForWinner()
        {
            if (OpponentSprite != null && OpponentSprite.Health <= 0)
            {
                OpponentSprite.Health = 0;
                State = BattleState.PlayerVictory;

                if (plugin.PlayerProfile.AttunedSpriteIDs.Contains(OpponentSprite.ID))
                {
                    return;
                }

                int defeatsNeeded;
                switch (OpponentSprite.Rarity)
                {
                    case RarityTier.Uncommon:
                        defeatsNeeded = 3;
                        break;
                    case RarityTier.Rare:
                        defeatsNeeded = 5;
                        break;
                    default: // Common
                        defeatsNeeded = 1;
                        break;
                }

                plugin.PlayerProfile.DefeatCounts.TryGetValue(OpponentSprite.ID, out var currentDefeats);
                currentDefeats++;

                if (currentDefeats >= defeatsNeeded)
                {
                    plugin.PlayerProfile.AttunedSpriteIDs.Add(OpponentSprite.ID);
                    plugin.PlayerProfile.DefeatCounts.Remove(OpponentSprite.ID);
                    Plugin.Log.Info($"Attuned with new Sprite: {OpponentSprite.Name} (ID: {OpponentSprite.ID})!");

                    int attunedCount = plugin.PlayerProfile.AttunedSpriteIDs.Count;
                    int potentialNewMaxAether = 10 + (attunedCount / 5);
                    int newMaxAether = Math.Min(20, potentialNewMaxAether);

                    if (newMaxAether > plugin.PlayerProfile.MaxAether)
                    {
                        plugin.PlayerProfile.MaxAether = newMaxAether;
                        plugin.PlayerProfile.CurrentAether++;
                        Plugin.Log.Info($"Max Aether increased to {newMaxAether}!");
                    }
                }
                else
                {
                    plugin.PlayerProfile.DefeatCounts[OpponentSprite.ID] = currentDefeats;
                    Plugin.Log.Info($"Defeated {OpponentSprite.Name}. Progress: {currentDefeats}/{defeatsNeeded}");
                }

                plugin.SaveManager.SaveProfile(plugin.PlayerProfile);
            }
            else if (PlayerSprite != null && PlayerSprite.Health <= 0)
            {
                PlayerSprite.Health = 0;
                State = BattleState.OpponentVictory;
            }
        }

        private void DetermineTurnOrder()
        {
            if (PlayerSprite == null || OpponentSprite == null) return;

            if (PlayerSprite.Speed > PlayerSprite.Speed)
            {
                CurrentTurn = BattleTurn.Player;
            }
            else if (OpponentSprite.Speed > PlayerSprite.Speed)
            {
                CurrentTurn = BattleTurn.Opponent;
            }
            else
            {
                var random = new Random();
                CurrentTurn = random.Next(0, 2) == 0 ? BattleTurn.Player : BattleTurn.Opponent;
            }
        }
    }
}

using AetherialArena.Models;
using System;

namespace AetherialArena.Core
{
    public class BattleManager
    {
        // Add a "None" state to represent when no battle is active.
        public enum BattleState { None, InProgress, PlayerVictory, OpponentVictory }
        public enum BattleTurn { Player, Opponent }

        public Sprite? PlayerSprite { get; private set; }
        public Sprite? OpponentSprite { get; private set; }
        public BattleTurn CurrentTurn { get; private set; }
        // The state will now correctly default to "None" (value 0).
        public BattleState State { get; private set; } = BattleState.None;

        public void StartBattle(Sprite playerSprite, Sprite opponentSprite)
        {
            PlayerSprite = playerSprite;
            OpponentSprite = opponentSprite;
            State = BattleState.InProgress;
            DetermineTurnOrder();
        }

        // This new method will be called when a battle ends.
        public void EndBattle()
        {
            State = BattleState.None;
            PlayerSprite = null;
            OpponentSprite = null;
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

            if (PlayerSprite.Speed > OpponentSprite.Speed)
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

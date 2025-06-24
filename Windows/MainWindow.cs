using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using AetherialArena.Core;
using AetherialArena.Models;
using AetherialArena.Services;

namespace AetherialArena.Windows
{
    public class MainWindow : Window, IDisposable
    {
        private readonly Plugin plugin;
        private readonly BattleManager battleManager;
        private readonly PlayerProfile playerProfile;
        private readonly EncounterManager encounterManager;
        private readonly AssetManager assetManager;

        public MainWindow(Plugin plugin) : base("Aetherial Arena###AetherialArenaMainWindow")
        {
            this.plugin = plugin;
            this.battleManager = plugin.BattleManager;
            this.playerProfile = plugin.PlayerProfile;
            this.encounterManager = plugin.EncounterManager;
            this.assetManager = plugin.AssetManager;
            this.SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(600, 400), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
        }

        public void Dispose() { }

        public override void Draw()
        {
            ImGui.Text($"Aether: {playerProfile.CurrentAether} / {playerProfile.MaxAether}");
            ImGui.Separator();
            ImGui.Spacing();

            switch (battleManager.State)
            {
                case BattleManager.BattleState.InProgress:
                    DrawBattleUI();
                    break;
                case BattleManager.BattleState.PlayerVictory:
                    DrawEndScreen("You win!");
                    break;
                case BattleManager.BattleState.OpponentVictory:
                    DrawEndScreen("You lose!");
                    break;
                case BattleManager.BattleState.None:
                default:
                    DrawMainMenuUI();
                    break;
            }
        }

        private void DrawMainMenuUI()
        {
            ImGui.Text("The arena is quiet.");
            bool hasAether = playerProfile.CurrentAether > 0;
            if (!hasAether) ImGui.BeginDisabled();
            if (ImGui.Button("Search for Battle"))
            {
                encounterManager.SearchForEncounter();
            }
            if (!hasAether)
            {
                ImGui.EndDisabled();
                ImGui.SameLine();
                ImGui.Text("(Not enough Aether)");
            }
        }

        private void DrawEndScreen(string message)
        {
            ImGui.Text(message);
            if (ImGui.Button("Return to Menu"))
            {
                battleManager.EndBattle();
            }
        }

        private void DrawBattleUI()
        {
            battleManager.Update();

            var player = battleManager.PlayerSprite;
            var opponent = battleManager.OpponentSprite;

            if (player == null || opponent == null) return;

            var contentRegion = ImGui.GetContentRegionAvail();
            var columnWidth = contentRegion.X / 2.0f;

            if (ImGui.BeginTable("BattleLayout", 2, ImGuiTableFlags.BordersInnerV))
            {
                ImGui.TableSetupColumn("PlayerColumn", ImGuiTableColumnFlags.WidthFixed, columnWidth);
                ImGui.TableSetupColumn("OpponentColumn", ImGuiTableColumnFlags.WidthFixed, columnWidth);
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);

                // Player Side
                ImGui.Text(player.Name);
                ImGui.ProgressBar((float)player.Health / player.MaxHealth, new Vector2(-1, 0), $"HP: {player.Health}/{player.MaxHealth}");
                ImGui.ProgressBar((float)player.Mana / player.MaxMana, new Vector2(-1, 0), $"MP: {player.Mana}/{player.MaxMana}");

                var playerIcon = assetManager.GetIcon(player.IconName);
                if (playerIcon != null)
                {
                    ImGui.Image(playerIcon.ImGuiHandle, new Vector2(100, 100));
                }
                else
                {
                    ImGui.Dummy(new Vector2(100, 100));
                }
                ImGui.Dummy(new Vector2(0, 50));

                bool isPlayerTurn = battleManager.CurrentTurn == BattleManager.BattleTurn.Player;
                if (!isPlayerTurn) ImGui.BeginDisabled();
                if (ImGui.Button("Attack", new Vector2(-1, 0))) { battleManager.PlayerAttack(); }
                if (ImGui.Button("Heal", new Vector2(-1, 0))) { /* TODO */ }
                if (ImGui.Button("Special", new Vector2(-1, 0))) { /* TODO */ }
                if (!isPlayerTurn) ImGui.EndDisabled();

                ImGui.TableSetColumnIndex(1);

                // Opponent Side
                ImGui.Text(opponent.Name);
                ImGui.ProgressBar((float)opponent.Health / opponent.MaxHealth, new Vector2(-1, 0), $"HP: {opponent.Health}/{opponent.MaxHealth}");
                ImGui.ProgressBar((float)opponent.Mana / opponent.MaxMana, new Vector2(-1, 0), $"MP: {opponent.Mana}/{opponent.MaxMana}");

                var opponentIcon = assetManager.GetIcon(opponent.IconName);
                if (opponentIcon != null)
                {
                    ImGui.Image(opponentIcon.ImGuiHandle, new Vector2(100, 100));
                }
                else
                {
                    ImGui.Dummy(new Vector2(100, 100));
                }
                ImGui.Dummy(new Vector2(0, 50));

                if (isPlayerTurn) { ImGui.Text("Player's Turn."); } else { ImGui.Text("Opponent's Turn..."); }

                ImGui.EndTable();
            }
        }
    }
}

using AetherialArena.Core;
using AetherialArena.Services;
using ImGuiNET;
using System.Numerics;

namespace AetherialArena.UI
{
    public class BattleUIComponent
    {
        private readonly BattleManager battleManager;
        private readonly AssetManager assetManager;

        public BattleUIComponent(BattleManager battleManager, AssetManager assetManager)
        {
            this.battleManager = battleManager;
            this.assetManager = assetManager;
        }

        public void Draw()
        {
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

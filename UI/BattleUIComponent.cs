using AetherialArena.Core;
using AetherialArena.Models;
using AetherialArena.Services;
using ImGuiNET;
using System.Linq;
using System.Numerics;

namespace AetherialArena.UI
{
    public class BattleUIComponent
    {
        private readonly BattleManager battleManager;
        private readonly AssetManager assetManager;
        private readonly DataManager dataManager;

        public BattleUIComponent(BattleManager battleManager, AssetManager assetManager, DataManager dataManager)
        {
            this.battleManager = battleManager;
            this.assetManager = assetManager;
            this.dataManager = dataManager;
        }

        public void Draw()
        {
            DrawMainBattleInterface();
            ImGui.Separator();
            DrawCombatLog();
        }

        private void DrawMainBattleInterface()
        {
            var activePlayer = battleManager.ActivePlayerSprite;
            var opponent = battleManager.OpponentSprite;
            if (activePlayer == null || opponent == null) return;

            var columnWidth = ImGui.GetContentRegionAvail().X / 2.0f;
            if (ImGui.BeginTable("BattleLayout", 2, ImGuiTableFlags.BordersInnerV))
            {
                ImGui.TableSetupColumn("PlayerColumn", ImGuiTableColumnFlags.WidthFixed, columnWidth);
                ImGui.TableSetupColumn("OpponentColumn", ImGuiTableColumnFlags.WidthFixed, columnWidth);
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                DrawSpritePanel(activePlayer);
                DrawActionButtons(activePlayer);
                DrawReservesPanel();
                ImGui.TableSetColumnIndex(1);
                DrawSpritePanel(opponent);
                DrawTurnStatus();
                ImGui.EndTable();
            }
        }

        private void DrawSpritePanel(Sprite sprite)
        {
            ImGui.Text(sprite.Name);
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(0.2f, 0.8f, 0.2f, 1.0f));
            ImGui.ProgressBar((float)sprite.Health / sprite.MaxHealth, new Vector2(-1, 0), $"HP: {sprite.Health}/{sprite.MaxHealth}");
            ImGui.PopStyleColor();

            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(0.2f, 0.2f, 0.8f, 1.0f));
            ImGui.ProgressBar((float)sprite.Mana / sprite.MaxMana, new Vector2(-1, 0), $"MP: {sprite.Mana}/{sprite.MaxMana}");
            ImGui.PopStyleColor();

            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(0.9f, 0.7f, 0.1f, 1.0f));
            float atb = (float)battleManager.GetActionGauge(sprite) / battleManager.GetMaxActionGauge();
            ImGui.ProgressBar(atb, new Vector2(-1, 0), "ATB");
            ImGui.PopStyleColor();

            var icon = assetManager.GetRecoloredIcon(sprite.IconName, sprite.RecolorKey, true);
            if (icon != null) ImGui.Image(icon.ImGuiHandle, new Vector2(100, 100));
            else ImGui.Dummy(new Vector2(100, 100));
        }

        private void DrawActionButtons(Sprite activePlayer)
        {
            ImGui.Dummy(new Vector2(0, 10));
            if (!battleManager.IsPlayerTurn) ImGui.BeginDisabled();

            if (ImGui.Button("Attack", new Vector2(-1, 0))) { battleManager.PlayerAttack(); }
            bool canHeal = activePlayer.Mana >= 10;
            if (!canHeal) ImGui.BeginDisabled();
            if (ImGui.Button("Heal", new Vector2(-1, 0))) { battleManager.PlayerHeal(); }
            if (!canHeal) ImGui.EndDisabled();
            var specialAbility = dataManager.GetAbility(activePlayer.SpecialAbilityID);
            bool canUseSpecial = specialAbility != null && activePlayer.Mana >= specialAbility.ManaCost;
            if (!canUseSpecial) ImGui.BeginDisabled();
            if (ImGui.Button("Special", new Vector2(-1, 0))) { battleManager.PlayerUseSpecial(); }
            if (!canUseSpecial) ImGui.EndDisabled();

            if (!battleManager.IsPlayerTurn) ImGui.EndDisabled();
        }

        private void DrawReservesPanel()
        {
            ImGui.Separator();
            ImGui.Text("Reserves:");
            var activePlayer = battleManager.ActivePlayerSprite;
            if (activePlayer == null) return;
            var reserveSprites = battleManager.PlayerParty.Where(s => s.ID != activePlayer.ID).ToList();
            if (!reserveSprites.Any())
            {
                ImGui.Text("None");
                return;
            }
            for (int i = 0; i < reserveSprites.Count; i++)
            {
                var reserve = reserveSprites[i];
                var tint = reserve.Health > 0 ? new Vector4(1, 1, 1, 1) : new Vector4(0.5f, 0.5f, 0.5f, 0.7f);
                var reserveIcon = assetManager.GetRecoloredIcon(reserve.IconName, reserve.RecolorKey, true);
                if (reserveIcon != null) ImGui.Image(reserveIcon.ImGuiHandle, new Vector2(40, 40), Vector2.Zero, Vector2.One, tint);
                else ImGui.Dummy(new Vector2(40, 40));
                if (i < reserveSprites.Count - 1) ImGui.SameLine();
            }
        }

        private void DrawTurnStatus()
        {
            ImGui.Dummy(new Vector2(0, 50));
            var activePlayer = battleManager.ActivePlayerSprite;
            if (activePlayer == null) return;
            if (battleManager.IsPlayerTurn) { ImGui.Text($"{activePlayer.Name}'s Turn."); } else { ImGui.Text("Waiting..."); }
        }

        private void DrawCombatLog()
        {
            ImGui.Text("Combat Log");
            float logHeight = ImGui.GetTextLineHeightWithSpacing() * 6;

            ImGui.BeginChild("CombatLogScrollingRegion", new Vector2(0, logHeight), true, ImGuiWindowFlags.HorizontalScrollbar);
            bool isScrolledToBottom = ImGui.GetScrollY() >= ImGui.GetScrollMaxY();
            foreach (var message in battleManager.CombatLog)
            {
                ImGui.TextWrapped(message);
            }
            if (isScrolledToBottom)
            {
                ImGui.SetScrollY(ImGui.GetScrollMaxY());
            }
            ImGui.EndChild();
        }
    }
}

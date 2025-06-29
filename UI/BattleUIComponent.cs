using AetherialArena.Core;
using AetherialArena.Models;
using AetherialArena.Services;
using ImGuiNET;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace AetherialArena.UI
{
    public class BattleUIComponent
    {
        private readonly BattleManager battleManager;
        private readonly AssetManager assetManager;
        private readonly DataManager dataManager;

        private readonly Dictionary<CombatLogColor, uint> colorMap = new()
        {
            { CombatLogColor.Normal, 0xFFFFFFFF },
            { CombatLogColor.Damage, 0xFF7979FF },
            { CombatLogColor.Heal,   0xFF79FF79 },
            { CombatLogColor.Status, 0xFF79FFFF }
        };

        public BattleUIComponent(BattleManager battleManager, AssetManager assetManager, DataManager dataManager)
        {
            this.battleManager = battleManager;
            this.assetManager = assetManager;
            this.dataManager = dataManager;
        }

        private void DrawTextWithOutline(string text, Vector2 pos, uint textColor, uint outlineColor)
        {
            var drawList = ImGui.GetWindowDrawList();
            var outlineOffset = new Vector2(1, 1);

            drawList.AddText(pos - outlineOffset, outlineColor, text);
            drawList.AddText(pos + new Vector2(outlineOffset.X, -outlineOffset.Y), outlineColor, text);
            drawList.AddText(pos + new Vector2(-outlineOffset.X, outlineOffset.Y), outlineColor, text);
            drawList.AddText(pos + outlineOffset, outlineColor, text);

            drawList.AddText(pos, textColor, text);
        }

        private void DrawWrappedOutlinedText(string text, float wrapWidth, uint color)
        {
            var words = text.Split(' ');
            if (words.Length == 0) return;

            var line = new StringBuilder();
            var firstWordOfLine = true;

            foreach (var word in words)
            {
                var tempLine = new StringBuilder(line.ToString());
                if (!firstWordOfLine)
                {
                    tempLine.Append(' ');
                }
                tempLine.Append(word);

                var lineWidth = ImGui.CalcTextSize(tempLine.ToString()).X;

                if (lineWidth > wrapWidth && !firstWordOfLine)
                {
                    var currentLineText = line.ToString();
                    var lineSize = ImGui.CalcTextSize(currentLineText);
                    DrawTextWithOutline(currentLineText, ImGui.GetCursorScreenPos(), color, 0xFF000000);
                    ImGui.Dummy(lineSize);
                    line.Clear();
                    line.Append(word);
                    firstWordOfLine = true;
                }
                else
                {
                    if (!firstWordOfLine)
                    {
                        line.Append(' ');
                    }
                    line.Append(word);
                    firstWordOfLine = false;
                }
            }

            if (line.Length > 0)
            {
                var remainingText = line.ToString();
                var lineSize = ImGui.CalcTextSize(remainingText);
                DrawTextWithOutline(remainingText, ImGui.GetCursorScreenPos(), color, 0xFF000000);
                ImGui.Dummy(lineSize);
            }
        }

        private void DrawOutlinedText(string text)
        {
            var textSize = ImGui.CalcTextSize(text);
            DrawTextWithOutline(text, ImGui.GetCursorScreenPos(), 0xFFFFFFFF, 0xFF000000);
            ImGui.Dummy(textSize);
        }

        private bool DrawButtonWithOutline(string id, string text, Vector2 size, uint textColor = 0xFFFFFFFF)
        {
            var clicked = ImGui.Button($"##{id}", size);
            var buttonPos = ImGui.GetItemRectMin();
            var buttonSize = ImGui.GetItemRectSize();
            var textSize = ImGui.CalcTextSize(text);
            var textPos = buttonPos + (buttonSize - textSize) * 0.5f;

            DrawTextWithOutline(text, textPos, textColor, 0xFF000000);

            return clicked;
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
            DrawOutlinedText(sprite.Name);

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

            var buttonSize = new Vector2(-1, 0);
            if (DrawButtonWithOutline("Attack", "Attack", buttonSize)) { battleManager.PlayerAttack(); }

            bool canHeal = activePlayer.Mana >= 10;
            uint healColor = canHeal ? 0xFFFFFFFF : 0xFF8080FF;
            if (!canHeal) ImGui.BeginDisabled();
            if (DrawButtonWithOutline("Heal", "Heal", buttonSize, healColor)) { battleManager.PlayerHeal(); }
            if (!canHeal) ImGui.EndDisabled();

            var specialAbility = dataManager.GetAbility(activePlayer.SpecialAbilityID);
            bool canUseSpecial = specialAbility != null && activePlayer.Mana >= specialAbility.ManaCost;
            uint specialColor = canUseSpecial ? 0xFFFFFFFF : 0xFF8080FF;
            if (!canUseSpecial) ImGui.BeginDisabled();
            if (DrawButtonWithOutline("Special", "Special", buttonSize, specialColor)) { battleManager.PlayerUseSpecial(); }
            if (!canUseSpecial) ImGui.EndDisabled();

            if (!battleManager.IsPlayerTurn) ImGui.EndDisabled();
        }

        private void DrawReservesPanel()
        {
            ImGui.Separator();
            DrawOutlinedText("Reserves:");
            var activePlayer = battleManager.ActivePlayerSprite;
            if (activePlayer == null) return;
            var reserveSprites = battleManager.PlayerParty.Where(s => s.ID != activePlayer.ID).ToList();
            if (!reserveSprites.Any())
            {
                DrawOutlinedText("None");
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
            var statusText = battleManager.IsPlayerTurn ? $"{activePlayer.Name}'s Turn." : "Waiting...";
            DrawOutlinedText(statusText);
        }

        private void DrawCombatLog()
        {
            DrawOutlinedText("Combat Log");
            float logHeight = ImGui.GetTextLineHeightWithSpacing() * 6;

            var childBg = ImGui.GetStyle().Colors[(int)ImGuiCol.ChildBg];
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(childBg.X, childBg.Y, childBg.Z, 0.5f));

            ImGui.BeginChild("CombatLogScrollingRegion", new Vector2(0, logHeight), true, ImGuiWindowFlags.HorizontalScrollbar);

            var wrapWidth = ImGui.GetContentRegionAvail().X;
            foreach (var logEntry in battleManager.CombatLog)
            {
                DrawWrappedOutlinedText(logEntry.Message, wrapWidth, colorMap[logEntry.Color]);
            }

            if (battleManager.ShouldScrollLog)
            {
                ImGui.SetScrollHereY(1.0f);
                battleManager.ConsumeScrollLogTrigger();
            }

            ImGui.EndChild();
            ImGui.PopStyleColor();
        }
    }
}

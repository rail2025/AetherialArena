using AetherialArena.Core;
using AetherialArena.Models;
using AetherialArena.Services;
using Dalamud.Plugin.Services;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace AetherialArena.UI
{
    public class BattleUIComponent
    {
        private readonly Plugin plugin;
        private readonly BattleManager battleManager;
        private readonly AssetManager assetManager;
        private readonly DataManager dataManager;
        private readonly IFramework framework;

        private float animationTimer = 0;
        private const float ANIMATION_DURATION = 0.6f;
        private const float HIT_DELAY = 0.2f;
        private Vector2 playerSpriteOffset = Vector2.Zero;
        private Vector2 opponentSpriteOffset = Vector2.Zero;

        private readonly List<(string iconName, Sprite target, bool isBossSpecial)> damageIconsToShow = new();
        private float damageIconTimer = 0;
        private const float DAMAGE_ICON_DURATION = 0.8f;
        private const float BOSS_DAMAGE_ICON_DURATION = 2.0f;

        private Sprite? currentAttacker;
        private Sprite? currentTarget;
        private bool isCurrentActionSelfBuff;

        private bool isSwapping = false;
        private bool isChoosingSpecial = false;

        private readonly Dictionary<CombatLogColor, uint> colorMap = new()
        {
            { CombatLogColor.Normal, 0xFFFFFFFF }, { CombatLogColor.Damage, 0xFF7979FF },
            { CombatLogColor.Heal, 0xFF79FF79 }, { CombatLogColor.Status, 0xFF79FFFF }
        };

        private readonly Dictionary<int, string> bossSpecialIcons = new()
        {
            { 101, "scorchingwhip_icon.png" }, { 102, "entomb_icon.png" },
            { 103, "galeforce_icon.png" }, { 104, "glacialedge_icon.png" },
            { 105, "tidallash_icon.png" }, { 106, "bolt_icon.png" },
            { 107, "totalannihilation_icon.png" }
        };

        public BattleUIComponent(Plugin plugin, IFramework framework)
        {
            this.plugin = plugin;
            this.framework = framework;
            this.battleManager = plugin.BattleManager;
            this.assetManager = plugin.AssetManager;
            this.dataManager = plugin.DataManager;
        }

        public void Update()
        {
            var deltaTime = (float)framework.UpdateDelta.TotalSeconds;

            if (!battleManager.IsPlayerTurn)
            {
                if (isSwapping) isSwapping = false;
                if (isChoosingSpecial) isChoosingSpecial = false;
            }

            if (battleManager.AnimationTrigger.HasValue)
            {
                var (iconName, displayOn) = battleManager.AnimationTrigger.Value;
                damageIconsToShow.Add((iconName, displayOn, true));
                damageIconTimer = BOSS_DAMAGE_ICON_DURATION;
                battleManager.ConsumeAnimationTrigger();
            }

            if (battleManager.AttackingSprite != null && animationTimer <= 0)
            {
                animationTimer = ANIMATION_DURATION;
                currentAttacker = battleManager.AttackingSprite;
                currentTarget = battleManager.TargetSprite;
                isCurrentActionSelfBuff = battleManager.IsSelfBuff;
                damageIconsToShow.Clear();

                bool isBossSpecial = battleManager.LastAbilityUsedId.HasValue && bossSpecialIcons.ContainsKey(battleManager.LastAbilityUsedId.Value);
                damageIconTimer = isBossSpecial ? BOSS_DAMAGE_ICON_DURATION : DAMAGE_ICON_DURATION;

                if (isBossSpecial)
                {
                    if (currentTarget != null)
                    {
                        
                        int abilityId = battleManager.LastAbilityUsedId!.Value;
                        damageIconsToShow.Add((bossSpecialIcons[abilityId], currentTarget, true));
                        
                    }
                }
                else if ((!battleManager.IsHealAction || isCurrentActionSelfBuff) && currentTarget != null)
                {
                    foreach (var attackType in battleManager.LastAttackTypes)
                    {
                        damageIconsToShow.Add(($"{attackType.ToLowerInvariant()}_icon.png", currentTarget, false));
                    }
                }
                battleManager.ClearLastAction();
            }

            if (animationTimer > 0)
            {
                animationTimer -= deltaTime;
            }
            else
            {
                currentAttacker = null;
                currentTarget = null;
                isCurrentActionSelfBuff = false;
            }

            if (damageIconTimer > 0) damageIconTimer -= deltaTime;

            playerSpriteOffset = Vector2.Zero;
            opponentSpriteOffset = Vector2.Zero;

            if (animationTimer > 0 && currentAttacker != null && !isCurrentActionSelfBuff)
            {
                bool isPlayerPartyAttacker = battleManager.PlayerParty.Contains(currentAttacker);
                float dashDistance = 40.0f;
                float flinchDistance = 20.0f;
                float attackProgress = 1 - (animationTimer / ANIMATION_DURATION);
                float attackOffset = (float)Math.Sin(attackProgress * Math.PI) * dashDistance;
                float hitOffset = 0;
                float elapsedTime = ANIMATION_DURATION - animationTimer;
                if (elapsedTime > HIT_DELAY)
                {
                    float flinchDuration = ANIMATION_DURATION - HIT_DELAY;
                    float flinchProgress = (elapsedTime - HIT_DELAY) / flinchDuration;
                    hitOffset = (float)Math.Sin(flinchProgress * Math.PI) * flinchDistance;
                }
                if (isPlayerPartyAttacker)
                {
                    playerSpriteOffset.X += attackOffset;
                    if (currentTarget == battleManager.OpponentSprite)
                    {
                        opponentSpriteOffset.X += hitOffset;
                    }
                }
                else
                {
                    opponentSpriteOffset.X -= attackOffset;
                    if (currentTarget == battleManager.ActivePlayerSprite)
                    {
                        playerSpriteOffset.X -= hitOffset;
                    }
                }
            }
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
                if (!firstWordOfLine) { tempLine.Append(' '); }
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
                    if (!firstWordOfLine) { line.Append(' '); }
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
            if (clicked) { plugin.AudioManager.PlaySfx("menuselect.wav"); }
            var buttonPos = ImGui.GetItemRectMin();
            var buttonSize = ImGui.GetItemRectSize();
            var textSize = ImGui.CalcTextSize(text);
            var textPos = buttonPos + (buttonSize - textSize) * 0.5f;
            DrawTextWithOutline(text, textPos, textColor, 0xFF000000);
            return clicked;
        }

        private bool DrawCheckboxWithOutline(string id, string text, ref bool isChecked)
        {
            var clicked = ImGui.Checkbox($"##{id}", ref isChecked);
            if (clicked) { plugin.AudioManager.PlaySfx("menuselect.wav"); }
            ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
            var textPos = ImGui.GetCursorScreenPos();
            textPos.Y += (ImGui.GetFrameHeight() - ImGui.GetTextLineHeight()) / 2;
            DrawTextWithOutline(text, textPos, 0xFFFFFFFF, 0xFF000000);
            ImGui.SameLine(0, ImGui.CalcTextSize(text).X + ImGui.GetStyle().ItemSpacing.X);
            ImGui.Dummy(new Vector2(0, ImGui.GetFrameHeight()));
            return clicked;
        }

        public void Draw()
        {
            DrawMainBattleInterface();
            ImGui.Separator();
            PublicDrawCombatLog();
            ImGui.Separator();
            DrawFooterControls();
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

                if (isSwapping)
                {
                    DrawSwapSelection();
                }
                else if (isChoosingSpecial)
                {
                    DrawSpecialSelection();
                }
                else
                {
                    DrawActionButtons(activePlayer);
                }

                DrawReservesPanel();

                ImGui.TableSetColumnIndex(1);
                DrawSpritePanel(opponent);
                DrawTurnStatus();
                ImGui.EndTable();
            }
        }

        private void DrawSpritePanel(Sprite sprite)
        {
            var isOpponent = battleManager.OpponentSprite == sprite;
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

            var iconSize = new Vector2(100, 100);
            var spaceToReserve = iconSize;

            if (sprite.Rarity == RarityTier.Boss)
            {
                iconSize = new Vector2(300, 300);
            }

            var baseDrawPos = ImGui.GetCursorScreenPos();
            var animationOffset = isOpponent ? opponentSpriteOffset : playerSpriteOffset;

            if (sprite.Rarity == RarityTier.Boss)
            {
                var columnWidth = ImGui.GetColumnWidth();
                baseDrawPos.X += (columnWidth - iconSize.X) / 2;
                baseDrawPos.Y -= iconSize.Y / 3;
            }
            else if (isOpponent)
            {
                var columnWidth = ImGui.GetColumnWidth();
                baseDrawPos.X += (columnWidth - iconSize.X - ImGui.GetStyle().CellPadding.X);
            }

            var finalDrawPos = baseDrawPos + animationOffset;
            var drawList = ImGui.GetWindowDrawList();
            var icon = assetManager.GetRecoloredIcon(sprite.IconName, sprite.RecolorKey, true);
            if (icon != null)
            {
                var uv0 = new Vector2(isOpponent ? 1 : 0, 0);
                var uv1 = new Vector2(isOpponent ? 0 : 1, 1);
                drawList.AddImage(icon.ImGuiHandle, finalDrawPos, finalDrawPos + iconSize, uv0, uv1);

                if (damageIconTimer > 0)
                {
                    foreach (var (iconName, target, isBossSpecial) in damageIconsToShow)
                    {
                        if (target == sprite)
                        {
                            var damageIcon = assetManager.GetIcon(iconName, true);
                            if (damageIcon != null)
                            {
                                var normalSize = new Vector2(98, 98) * plugin.Configuration.CustomUiScale;
                                var overlaySize = isBossSpecial ? normalSize * 3 : normalSize;
                                var overlayPos = finalDrawPos + (iconSize - overlaySize) / 2;
                                drawList.AddImage(damageIcon.ImGuiHandle, overlayPos, overlayPos + overlaySize);
                            }
                        }
                    }
                }
            }
            ImGui.Dummy(spaceToReserve);
        }

        private void DrawActionButtons(Sprite activePlayer)
        {
            ImGui.Dummy(new Vector2(0, 10));

            bool shouldBeDisabled = !battleManager.IsPlayerTurn;

            if (shouldBeDisabled)
            {
                ImGui.BeginDisabled();
            }

            var buttonWidth = ImGui.GetContentRegionAvail().X * 0.5f - ImGui.GetStyle().ItemSpacing.X * 0.5f;
            var buttonSize = new Vector2(buttonWidth, 0);

            if (DrawButtonWithOutline("Attack", "Attack", buttonSize)) { battleManager.PlayerAttack(); }

            ImGui.SameLine();

            bool canHeal = activePlayer.Mana >= 10;
            if (!canHeal) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * 0.5f);
            if (DrawButtonWithOutline("Heal", "Heal", buttonSize))
            {
                if (canHeal) battleManager.PlayerHeal();
            }
            if (!canHeal) ImGui.PopStyleVar();

            if (DrawButtonWithOutline("Swap", "Swap", buttonSize))
            {
                isSwapping = true;
            }

            ImGui.SameLine();

            bool canUseAnySpecial = activePlayer.SpecialAbilityIDs.Any(id =>
            {
                var ab = dataManager.GetAbility(id);
                return ab != null && activePlayer.Mana >= ab.ManaCost;
            });

            if (!canUseAnySpecial) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * 0.5f);

            if (DrawButtonWithOutline("Special", "Special", buttonSize))
            {
                if (canUseAnySpecial)
                {
                    if (activePlayer.SpecialAbilityIDs.Count > 1)
                    {
                        isChoosingSpecial = true;
                    }
                    else if (activePlayer.SpecialAbilityIDs.Count == 1)
                    {
                        battleManager.PlayerUseSpecial(activePlayer.SpecialAbilityIDs[0]);
                    }
                }
            }
            if (!canUseAnySpecial) ImGui.PopStyleVar();

            if (shouldBeDisabled)
            {
                ImGui.EndDisabled();
            }
        }

        private void DrawSwapSelection()
        {
            DrawOutlinedText("Select a Sprite to Swap In:");
            ImGui.BeginChild("SwapSelection", new Vector2(0, 80), true);

            var reserveSprites = battleManager.PlayerParty.Where(s => s.ID != battleManager.ActivePlayerSprite?.ID).ToList();

            if (!reserveSprites.Any())
            {
                DrawOutlinedText("No reserves available.");
            }
            else
            {
                foreach (var reserve in reserveSprites)
                {
                    bool canSwap = reserve.Health > 0;
                    if (!canSwap) ImGui.BeginDisabled();

                    if (ImGui.Selectable($"{reserve.Name} (HP: {reserve.Health}/{reserve.MaxHealth})##{reserve.ID}"))
                    {
                        battleManager.PlayerSwap(reserve.ID);
                        isSwapping = false;
                    }

                    if (!canSwap) ImGui.EndDisabled();
                }
            }
            ImGui.EndChild();

            if (DrawButtonWithOutline("CancelSwap", "Cancel", new Vector2(-1, 0)))
            {
                isSwapping = false;
            }
        }

        private void DrawSpecialSelection()
        {
            var activePlayer = battleManager.ActivePlayerSprite;
            if (activePlayer == null) return;

            DrawOutlinedText("Select a Special Ability:");
            ImGui.BeginChild("SpecialSelection", new Vector2(0, 80), true);

            foreach (var abilityId in activePlayer.SpecialAbilityIDs)
            {
                var ability = dataManager.GetAbility(abilityId);
                if (ability != null)
                {
                    bool canUse = activePlayer.Mana >= ability.ManaCost;
                    if (!canUse) ImGui.BeginDisabled();

                    if (ImGui.Selectable($"{ability.Name} (MP: {ability.ManaCost})##{ability.ID}"))
                    {
                        battleManager.PlayerUseSpecial(ability.ID);
                        isChoosingSpecial = false;
                    }

                    if (!canUse) ImGui.EndDisabled();
                }
            }
            ImGui.EndChild();

            if (DrawButtonWithOutline("CancelSpecial", "Cancel", new Vector2(-1, 0)))
            {
                isChoosingSpecial = false;
            }
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
                else ImGui.Dummy(new Vector2(40, 40) * plugin.Configuration.CustomUiScale);
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

        public void PublicDrawCombatLog()
        {
            DrawOutlinedText("Combat Log");
            float logHeight = ImGui.GetTextLineHeightWithSpacing() * 5;
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

        private void DrawFooterControls()
        {
            var fleeText = "Flee combat and lose aether";
            var fleeButtonSize = ImGui.CalcTextSize(fleeText) + ImGui.GetStyle().FramePadding * 2;
            if (DrawButtonWithOutline("Flee", fleeText, new Vector2(fleeButtonSize.X, 0)))
            {
                plugin.AudioManager.EndPlaylist();
                plugin.AudioManager.PlayMusic("titlemusic.mp3", true);
                battleManager.FleeBattle();
            }
            var musicText = "Mute Music";
            var sfxText = "Mute SFX";
            var musicCheckWidth = ImGui.CalcTextSize(musicText).X + ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemInnerSpacing.X;
            var sfxCheckWidth = ImGui.CalcTextSize(sfxText).X + ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemInnerSpacing.X;
            var totalCheckboxWidth = musicCheckWidth + sfxCheckWidth + ImGui.GetStyle().ItemSpacing.X;
            ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - totalCheckboxWidth);
            ImGui.BeginGroup();
            bool musicMuted = plugin.Configuration.IsBgmMuted;
            if (DrawCheckboxWithOutline("MuteMusicBattle", musicText, ref musicMuted))
            {
                plugin.Configuration.IsBgmMuted = musicMuted;
                plugin.Configuration.Save();
                plugin.AudioManager.UpdateBgmState();
            }
            ImGui.SameLine();
            bool sfxMuted = plugin.Configuration.IsSfxMuted;
            if (DrawCheckboxWithOutline("MuteSfxBattle", sfxText, ref sfxMuted))
            {
                plugin.Configuration.IsSfxMuted = sfxMuted;
                plugin.Configuration.Save();
            }
            ImGui.EndGroup();
        }
    }
}

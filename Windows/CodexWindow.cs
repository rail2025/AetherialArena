using System;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Dalamud.Interface.Windowing;
using AetherialArena.Models;
using AetherialArena.Services;
using Dalamud.Interface.Textures.TextureWraps;
using System.Collections.Generic;

namespace AetherialArena.Windows
{
    public class CodexWindow : Window
    {
        private enum CodexState
        {
            List,
            Details
        }

        private CodexState currentState = CodexState.List;
        private Sprite? selectedSprite;

        private readonly DataManager dataManager;
        private readonly PlayerProfile playerProfile;
        private readonly Plugin plugin;
        private readonly AssetManager assetManager;

        private const int RowsPerPage = 5;
        private int currentPage = 0;

        public CodexWindow(Plugin plugin) : base("Codex###AetherialArenaCodexWindow")
        {
            this.dataManager = plugin.DataManager;
            this.plugin = plugin;
            this.playerProfile = plugin.PlayerProfile;
            this.assetManager = plugin.AssetManager;

            this.Size = new Vector2(640, 535);
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(400, 300),
                MaximumSize = new Vector2(1280, 1024)
            };

            this.Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar;
        }

        public override void PreDraw()
        {
            var baseSize = new Vector2(640, 535);
            this.Size = baseSize * plugin.Configuration.CustomUiScale;
            this.Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar;
        }

        public override void Draw()
        {
            var backgroundTexture = this.assetManager.GetIcon("aacodex.png");
            if (backgroundTexture != null)
            {
                var windowPos = ImGui.GetWindowPos();
                var windowSize = ImGui.GetWindowSize();
                ImGui.GetWindowDrawList().AddImage(backgroundTexture.ImGuiHandle, windowPos, windowPos + windowSize);
            }

            var contentRegion = ImGui.GetContentRegionAvail();
            var containerSize = new Vector2(400, 280);
            var containerPos = (contentRegion - containerSize) / 2;
            ImGui.SetCursorPos(containerPos);

            ImGui.BeginChild("ContentContainer", containerSize, false, ImGuiWindowFlags.NoScrollbar);

            switch (currentState)
            {
                case CodexState.List:
                    DrawListView();
                    break;
                case CodexState.Details:
                    DrawDetailsView();
                    break;
            }

            ImGui.EndChild();
        }

        private void DrawListView()
        {
            DrawSpriteTable();
            DrawPaginationControls();
        }

        private void DrawDetailsView()
        {
            if (selectedSprite == null)
            {
                currentState = CodexState.List;
                return;
            }

            var s = selectedSprite;
            playerProfile.CapturedSpriteData.TryGetValue(s.ID, out var playerData);

            var nameSize = ImGui.CalcTextSize(s.Name);
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - nameSize.X) / 2);
            ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.9f, 1.0f), s.Name);

            if (playerData != null)
            {
                string expString = playerData.Level >= 5 ? "MAX" : $"{playerData.Experience} / {GetExpToNextLevel(playerData.Level)}";
                string levelInfo = $"Level: {playerData.Level} | EXP: {expString}";
                var levelInfoSize = ImGui.CalcTextSize(levelInfo);
                ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - levelInfoSize.X) / 2);
                ImGui.Text(levelInfo);
            }

            var typeInfo = $"{s.Rarity} / {s.Type}" + (string.IsNullOrEmpty(s.SubType) ? "" : $" / {s.SubType}");
            var typeInfoSize = ImGui.CalcTextSize(typeInfo);
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - typeInfoSize.X) / 2);
            ImGui.TextColored(new Vector4(1.0f, 1.0f, 1.0f, 1.0f), typeInfo);
            ImGui.Separator();

            ImGui.Text("Stats");
            ImGui.Indent();
            ImGui.Columns(2, "StatsColumns", false);

            // --- Column 1 ---
            ImGui.Text($"HP: {s.MaxHealth + (playerData?.AllocatedHP ?? 0)}");
            ImGui.Text($"Attack: {s.Attack + (playerData?.AllocatedAttack ?? 0)}");
            ImGui.Text($"Speed: {s.Speed + (playerData?.AllocatedSpeed ?? 0)}");

            // --- Column 2 ---
            ImGui.NextColumn();
            ImGui.Text($"MP: {s.MaxMana + (playerData?.AllocatedMP ?? 0)}");
            ImGui.Text($"Defense: {s.Defense + (playerData?.AllocatedDefense ?? 0)}");

            // --- "Assign Stats" button placed in the empty space ---
            ImGui.Spacing();
            if (playerData != null)
            {
                if (ImGui.Button("Assign Stats##DetailView", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
                {
                    plugin.UpgradeWindow.Open(selectedSprite);
                    this.IsOpen = false; // Close codex to prevent state issues
                }
            }

            ImGui.Columns(1);
            ImGui.Unindent();
            ImGui.Separator();

            ImGui.Text("Combat Details");
            ImGui.Indent();
            if (!string.IsNullOrWhiteSpace(s.SpecialAbility) && s.SpecialAbility != "None")
            {
                ImGui.TextWrapped($"Special: {s.SpecialAbility}");
            }
            ImGui.TextWrapped($"Attack Type: {string.Join(", ", s.AttackType)}");
            ImGui.TextWrapped($"Weaknesses: {string.Join(", ", s.Weaknesses)}");
            ImGui.TextWrapped($"Resistances: {string.Join(", ", s.Resistances)}");
            ImGui.Unindent();
            ImGui.Separator();

            // This button is now at the very bottom, physically separate from the other button
            if (ImGui.Button("Back to List##CodexDetailsBack", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
            {
                selectedSprite = null;
                currentState = CodexState.List;
            }
        }

        private void DrawSpriteTable()
        {
            ImGui.BeginChild("TableContainer", new Vector2(0, 240), false, ImGuiWindowFlags.None);
            if (ImGui.BeginTable("codexTable", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollY))
            {
                ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 30);
                ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, 45);
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Rarity", ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableSetupColumn("Status / Location", ImGuiTableColumnFlags.WidthStretch);

                var pagedSprites = dataManager.Sprites.Skip(currentPage * RowsPerPage).Take(RowsPerPage).ToList();
                foreach (var sprite in pagedSprites)
                {
                    ImGui.TableNextRow();
                    bool isKnown = playerProfile.AttunedSpriteIDs.Contains(sprite.ID) || playerProfile.DefeatCounts.ContainsKey(sprite.ID);

                    ImGui.TableSetColumnIndex(0);
                    if (ImGui.Selectable($"##{sprite.ID}", false, ImGuiSelectableFlags.SpanAllColumns | ImGuiSelectableFlags.AllowItemOverlap))
                    {
                        if (isKnown)
                        {
                            selectedSprite = sprite;
                            currentState = CodexState.Details;
                        }
                    }

                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text(sprite.ID.ToString());

                    ImGui.TableSetColumnIndex(1);
                    IDalamudTextureWrap? icon;
                    if (isKnown) { icon = assetManager.GetRecoloredIcon(sprite.IconName, sprite.RecolorKey); }
                    else { icon = assetManager.GetIcon("placeholder_icon.png"); }
                    if (icon != null) { ImGui.Image(icon.ImGuiHandle, new Vector2(40, 40)); } else { ImGui.Dummy(new Vector2(40, 40)); }

                    ImGui.TableSetColumnIndex(2);
                    ImGui.Text(isKnown ? sprite.Name : "???");

                    ImGui.TableSetColumnIndex(3);
                    ImGui.Text(isKnown ? sprite.Rarity.ToString() : "???");

                    ImGui.TableSetColumnIndex(4);
                    ImGui.TextWrapped(GetStatusOrLocation(sprite));
                }
                ImGui.EndTable();
            }
            ImGui.EndChild();
        }

        private void DrawPaginationControls()
        {
            ImGui.Spacing();
            var maxPage = Math.Max(0, (int)Math.Ceiling((double)dataManager.Sprites.Count / RowsPerPage) - 1);
            bool atFirstPage = currentPage == 0;
            bool atLastPage = currentPage == maxPage;
            var controlsWidth = 260f;
            ImGui.SetCursorPosX((ImGui.GetContentRegionAvail().X - controlsWidth) * 0.5f);
            ImGui.BeginGroup();
            if (atFirstPage) ImGui.BeginDisabled();
            if (ImGui.Button("<<##FirstPage")) { currentPage = 0; }
            if (atFirstPage) ImGui.EndDisabled();
            ImGui.SameLine();
            if (atFirstPage) ImGui.BeginDisabled();
            if (ImGui.Button("< Prev")) { currentPage--; }
            if (atFirstPage) ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50);
            if (ImGui.InputInt("##Page", ref currentPage)) { currentPage = Math.Clamp(currentPage, 0, maxPage); }
            ImGui.SameLine();
            ImGui.Text($"of {maxPage + 1}");
            ImGui.SameLine();
            if (atLastPage) ImGui.BeginDisabled();
            if (ImGui.Button("Next >")) { currentPage++; }
            if (atLastPage) ImGui.EndDisabled();
            ImGui.SameLine();
            if (atLastPage) ImGui.BeginDisabled();
            if (ImGui.Button(">>##LastPage")) { currentPage = maxPage; }
            if (atLastPage) ImGui.EndDisabled();
            ImGui.EndGroup();
        }

        private string GetStatusOrLocation(Sprite sprite)
        {
            if (playerProfile.AttunedSpriteIDs.Contains(sprite.ID))
                return "Captured";

            if (playerProfile.DefeatCounts.TryGetValue(sprite.ID, out int defeatCount))
            {
                int defeatsNeeded = GetDefeatsNeeded(sprite.Rarity);
                return $"{defeatCount} / {defeatsNeeded}";
            }

            if (sprite.Rarity == RarityTier.Common)
            {
                string location = dataManager.GetLocation(sprite.ID);
                if (!string.IsNullOrEmpty(location))
                {
                    return location;
                }
            }

            string hint = dataManager.GetHint(sprite.ID);
            return !string.IsNullOrEmpty(hint) ? hint : "Not Found";
        }

        private int GetDefeatsNeeded(RarityTier rarity)
        {
            return rarity switch
            {
                RarityTier.Uncommon => 3,
                RarityTier.Rare => 5,
                _ => 1,
            };
        }

        private int GetExpToNextLevel(int currentLevel)
        {
            if (currentLevel >= 5) return 0;
            int[] expToLevel = { 0, 40, 60, 80, 100 };
            return expToLevel[currentLevel];
        }
    }
}

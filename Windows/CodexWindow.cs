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
        private readonly DataManager dataManager;
        private readonly PlayerProfile playerProfile;
        private readonly AssetManager assetManager;
        private readonly IDalamudTextureWrap? backgroundTexture;
        private readonly Dictionary<int, string> locationHints = new();

        private const int RowsPerPage = 5;
        private int currentPage = 0;

        public CodexWindow(Plugin plugin) : base("Codex###AetherialArenaCodexWindow")
        {
            this.dataManager = plugin.DataManager;
            this.playerProfile = plugin.PlayerProfile;
            this.assetManager = plugin.AssetManager;
            this.backgroundTexture = this.assetManager.GetIcon("aacodex.png");

            this.Size = new Vector2(640, 535);
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(400, 300),
                MaximumSize = new Vector2(1280, 1024)
            };

            LoadHints();
            this.Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar;
        }

        public override void Draw()
        {
            if (this.backgroundTexture != null)
            {
                var windowPos = ImGui.GetWindowPos();
                var windowSize = ImGui.GetWindowSize();
                ImGui.GetWindowDrawList().AddImage(this.backgroundTexture.ImGuiHandle, windowPos, windowPos + windowSize);
            }

            var contentRegion = ImGui.GetContentRegionAvail();
            var containerSize = new Vector2(400, 280);
            var containerPos = (contentRegion - containerSize) / 2;
            ImGui.SetCursorPos(containerPos);

            ImGui.BeginChild("ContentContainer", containerSize, false, ImGuiWindowFlags.NoScrollbar);
            DrawSpriteTable();
            DrawPaginationControls();
            ImGui.EndChild();
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
                ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthStretch);

                var pagedSprites = dataManager.Sprites.Skip(currentPage * RowsPerPage).Take(RowsPerPage).ToList();
                foreach (var sprite in pagedSprites)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text(sprite.ID.ToString());

                    ImGui.TableSetColumnIndex(1);
                    var icon = assetManager.GetIcon(sprite.IconName);
                    if (icon != null) { ImGui.Image(icon.ImGuiHandle, new Vector2(40, 40)); } else { ImGui.Dummy(new Vector2(40, 40)); }

                    bool isKnown = playerProfile.AttunedSpriteIDs.Contains(sprite.ID) || playerProfile.DefeatCounts.ContainsKey(sprite.ID);

                    ImGui.TableSetColumnIndex(2);
                    ImGui.Text(isKnown ? sprite.Name : "???");

                    ImGui.TableSetColumnIndex(3);
                    ImGui.Text(isKnown ? sprite.Rarity.ToString() : "???");

                    ImGui.TableSetColumnIndex(4);
                    ImGui.TextWrapped(GetStatus(sprite));
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

        private void LoadHints()
        {
            locationHints[4] = "a bunch of lunatics!";
            locationHints[5] = "Lookout!";
            locationHints[9] = "ant ant ant";
            locationHints[10] = "gem exchange";
            locationHints[14] = "light on a hill";
            locationHints[15] = "long way down!";
            locationHints[19] = "Infernal troupe";
            locationHints[20] = "drill baby drill";
            locationHints[24] = "yellow formation";
            locationHints[25] = "thats a lotta bull";
            locationHints[29] = "if you want some eggs...";
            locationHints[30] = "that's not a big scary hole...";
            locationHints[34] = "guiding light";
            locationHints[35] = "of course theres one all the way out there";
            locationHints[39] = "Cliffside skull";
            locationHints[40] = "hovering colors";
            locationHints[44] = "halitosis";
            locationHints[45] = "saddest wings on a lizard ever";
            locationHints[49] = "2 become 1";
            locationHints[50] = "purple, no lavender, no lilac! is my color";
            locationHints[54] = "Iron cutting sword";
            locationHints[55] = "hot air balloon";
            locationHints[59] = "mead lover";
            locationHints[60] = "judge not lest ye be";
            locationHints[64] = "is that a carnival ride or a telescope";
            locationHints[65] = "mountain tunnel";
            locationHints[69] = "Shard gate";
            locationHints[70] = "moon bride memorial";
        }

        private string GetHint(int spriteId)
        {
            return locationHints.GetValueOrDefault(spriteId, string.Empty);
        }

        private string GetStatus(Sprite sprite)
        {
            if (playerProfile.AttunedSpriteIDs.Contains(sprite.ID))
                return "Captured";

            if (playerProfile.DefeatCounts.TryGetValue(sprite.ID, out int defeatCount))
            {
                int defeatsNeeded = GetDefeatsNeeded(sprite.Rarity);
                return $"{defeatCount} / {defeatsNeeded}";
            }

            var hint = GetHint(sprite.ID);
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
    }
}

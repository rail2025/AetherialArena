using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using AetherialArena.Services;
using AetherialArena.Models;
using System.Linq;

namespace AetherialArena.Windows
{
    public class CollectionWindow : Window, IDisposable
    {
        private readonly Plugin plugin;
        private readonly DataManager dataManager;
        private readonly PlayerProfile playerProfile;
        private readonly AssetManager assetManager;

        public CollectionWindow(Plugin plugin) : base("My Collection###AetherialArenaCollectionWindow")
        {
            this.plugin = plugin;
            this.dataManager = plugin.DataManager;
            this.playerProfile = plugin.PlayerProfile;
            this.assetManager = plugin.AssetManager;

            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(400, 400),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
        }

        public void Dispose() { }

        // The Draw method is completely overhauled to show all sprites
        public override void Draw()
        {
            if (ImGui.BeginTable("CollectionTable", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit))
            {
                // Setup table columns
                ImGui.TableSetupColumn("R", ImGuiTableColumnFlags.WidthFixed, 20);
                ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, 50);
                ImGui.TableSetupColumn("Name");
                ImGui.TableSetupColumn("Status");
                ImGui.TableHeadersRow();

                // Loop through every sprite in the game's data
                foreach (var sprite in dataManager.Sprites)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);

                    // --- 1. Rarity Indicator ---
                    var (rarityChar, rarityColor) = GetRarityDisplay(sprite.Rarity);
                    ImGui.TextColored(rarityColor, rarityChar);

                    // --- Determine Sprite Status ---
                    bool isAttuned = playerProfile.AttunedSpriteIDs.Contains(sprite.ID);
                    bool inProgress = playerProfile.DefeatCounts.TryGetValue(sprite.ID, out var defeatCount);

                    ImGui.TableSetColumnIndex(1);

                    // --- 2. Icon ---
                    var icon = assetManager.GetRecoloredIcon(sprite.IconName, sprite.RecolorKey);
                    if (icon != null)
                    {
                        // Show full color icon if attuned, otherwise grey it out
                        var tint = isAttuned ? new Vector4(1, 1, 1, 1) : new Vector4(0.3f, 0.3f, 0.3f, 1);
                        ImGui.Image(icon.ImGuiHandle, new Vector2(40, 40), Vector2.Zero, Vector2.One, tint);
                    }
                    else
                    {
                        ImGui.Dummy(new Vector2(40, 40));
                    }

                    ImGui.TableSetColumnIndex(2);

                    // --- 3. Name ---
                    // Show "???" if the player has never defeated it once
                    ImGui.Text(isAttuned || inProgress ? sprite.Name : "???");

                    ImGui.TableSetColumnIndex(3);

                    // --- 4. Status ---
                    if (isAttuned)
                    {
                        ImGui.Text("Captured!");
                    }
                    else if (inProgress)
                    {
                        int defeatsNeeded = GetDefeatsNeeded(sprite.Rarity);
                        ImGui.Text($"{defeatCount} / {defeatsNeeded}");
                    }
                    else
                    {
                        ImGui.Text("Not Found");
                    }
                }

                ImGui.EndTable();
            }
        }

        // Helper function to get display character and color for rarity
        private (string, Vector4) GetRarityDisplay(RarityTier rarity)
        {
            return rarity switch
            {
                RarityTier.Uncommon => ("U", new Vector4(1.0f, 1.0f, 0.0f, 1.0f)), // Yellow
                RarityTier.Rare => ("R", new Vector4(0.5f, 0.5f, 1.0f, 1.0f)), // Blue
                _ => ("C", new Vector4(1.0f, 1.0f, 1.0f, 1.0f)), // White
            };
        }

        // Helper function to get the capture requirement for a given rarity
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

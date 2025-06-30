using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using AetherialArena.Services;
using AetherialArena.Models;
using System.Linq;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace AetherialArena.Windows
{
    public unsafe class CollectionWindow : Window, IDisposable
    {
        private readonly Plugin plugin;
        private readonly DataManager dataManager;
        private readonly PlayerProfile playerProfile;
        private readonly AssetManager assetManager;
        private readonly UIState* uiState;

        public CollectionWindow(Plugin plugin) : base("My Collection###AetherialArenaCollectionWindow")
        {
            this.plugin = plugin;
            this.dataManager = plugin.DataManager;
            this.playerProfile = plugin.PlayerProfile;
            this.assetManager = plugin.AssetManager;
            this.uiState = UIState.Instance();

            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(550, 400),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
        }

        public void Dispose() { }

        public override void Draw()
        {
            if (ImGui.BeginTable("CollectionTable", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.ScrollY))
            {
                ImGui.TableSetupColumn("R", ImGuiTableColumnFlags.WidthFixed, 20);
                ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, 50);
                ImGui.TableSetupColumn("Name");
                ImGui.TableSetupColumn("Capture Status");
                ImGui.TableSetupColumn("Minion Required", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableHeadersRow();

                foreach (var sprite in dataManager.Sprites.OrderBy(s => s.ID))
                {
                    bool isCaptured = playerProfile.AttunedSpriteIDs.Contains(sprite.ID);
                    bool isMinionOwned = false;
                    string minionToUnlock = "N/A";

                    if (dataManager.MinionUnlockMap.TryGetValue(sprite.ID, out var minionData))
                    {
                        minionToUnlock = minionData.Name;
                        if (uiState != null)
                        {
                            isMinionOwned = uiState->IsCompanionUnlocked(minionData.Id);
                        }
                    }

                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);

                    var (rarityChar, rarityColor) = GetRarityDisplay(sprite.Rarity);
                    ImGui.TextColored(rarityColor, rarityChar);

                    ImGui.TableSetColumnIndex(1);

                    var icon = assetManager.GetRecoloredIcon(sprite.IconName, sprite.RecolorKey);
                    if (icon != null)
                    {
                        // Icon is visible if minion is owned
                        var tint = isMinionOwned ? new Vector4(1, 1, 1, 1) : new Vector4(0.3f, 0.3f, 0.3f, 1);
                        ImGui.Image(icon.ImGuiHandle, new Vector2(40, 40), Vector2.Zero, Vector2.One, tint);
                    }
                    else
                    {
                        ImGui.Dummy(new Vector2(40, 40));
                    }

                    ImGui.TableSetColumnIndex(2);
                    ImGui.Text(isMinionOwned ? sprite.Name : "???");

                    ImGui.TableSetColumnIndex(3);

                    // Show capture status only if the sprite has been revealed by owning the minion
                    if (isMinionOwned)
                    {
                        if (isCaptured)
                        {
                            ImGui.Text("Captured!");
                        }
                        else if (playerProfile.DefeatCounts.TryGetValue(sprite.ID, out var defeatCount))
                        {
                            int defeatsNeeded = GetDefeatsNeeded(sprite.Rarity);
                            ImGui.Text($"{defeatCount} / {defeatsNeeded}");
                        }
                        else
                        {
                            ImGui.Text("Not Found");
                        }
                    }
                    else
                    {
                        ImGui.Text("---");
                    }


                    ImGui.TableSetColumnIndex(4);
                    string unlockStatus = isMinionOwned ? "(Owned)" : "(Missing)";
                    ImGui.Text($"{minionToUnlock} {unlockStatus}");
                }

                ImGui.EndTable();
            }
        }

        private (string, Vector4) GetRarityDisplay(RarityTier rarity)
        {
            return rarity switch
            {
                RarityTier.Uncommon => ("U", new Vector4(0.4f, 1.0f, 0.4f, 1.0f)),
                RarityTier.Rare => ("R", new Vector4(0.5f, 0.5f, 1.0f, 1.0f)),
                _ => ("C", new Vector4(0.8f, 0.8f, 0.8f, 1.0f)),
            };
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

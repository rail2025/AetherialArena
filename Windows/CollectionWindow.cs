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
using Dalamud.Interface.Textures.TextureWraps;

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

        public override void PreDraw()
        {
            var baseSize = new Vector2(640, 535);
            this.Size = baseSize * plugin.Configuration.CustomUiScale;
            this.Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar;
        }


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
                    bool hasProgress = playerProfile.DefeatCounts.ContainsKey(sprite.ID);
                    bool isKnown = isCaptured || hasProgress;
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
                    ImGui.TableSetColumnIndex(0); //Rarity

                    var (rarityChar, rarityColor) = GetRarityDisplay(sprite.Rarity);
                    ImGui.TextColored(rarityColor, rarityChar);

                    ImGui.TableSetColumnIndex(1); //Icon

                    IDalamudTextureWrap? icon;
                    if (isKnown)
                    {
                        icon = assetManager.GetRecoloredIcon(sprite.IconName, sprite.RecolorKey);
                    }
                    else
                    {
                        icon = assetManager.GetIcon("placeholder_icon.png");
                    }

                    if (icon != null)
                    {
                        ImGui.Image(icon.ImGuiHandle, new Vector2(40, 40));
                    }
                    else
                    {
                        ImGui.Dummy(new Vector2(40, 40));
                    }

                    ImGui.TableSetColumnIndex(2); //Name
                    ImGui.Text(isKnown ? sprite.Name : "???");

                    ImGui.TableSetColumnIndex(3); // Capture Status

                    
                    if (isKnown)
                    {
                        if (isCaptured)
                        {
                            ImGui.Text("Captured!");
                        }
                        else // hasProgress must be true
                        {
                            int defeatsNeeded = GetDefeatsNeeded(sprite.Rarity);
                            ImGui.Text($"{playerProfile.DefeatCounts[sprite.ID]} / {defeatsNeeded}");
                        }
                    }
                    else
                    {
                        ImGui.Text("Not Found");
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

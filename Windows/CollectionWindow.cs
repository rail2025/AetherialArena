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
        private readonly DataManager dataManager;
        private readonly PlayerProfile playerProfile;
        private readonly AssetManager assetManager;
        private readonly UIState* uiState;

        private readonly Dictionary<int, (string Name, uint Id)> minionUnlockMap = new()
        {
            { 1, ("Wind-up Airship", 52) }, { 2, ("Goobbue Sproutling", 41) }, { 3, ("Buffalo Calf", 32) },
            { 4, ("Midgardsormr", 119) }, { 5, ("Morbol Seedling", 12) }, { 6, ("Wind-up Onion Knight", 92) },
            { 7, ("Puff of Darkness", 101) }, { 8, ("Wind-up Cursor", 51) }, { 9, ("Black Chocobo Chick", 54) },
            { 10, ("Minion of Light", 67) }, { 11, ("Wide-eyed Fawn", 17) }, { 12, ("Dust Bunny", 28) },
            { 13, ("Beady Eye", 36) }, { 14, ("Princely Hatchling", 75) }, { 15, ("Fledgling Dodo", 37) },
            { 16, ("Coeurl Kitten", 19) }, { 17, ("Wind-up Leader", 71) }, { 18, ("Wind-up Odin", 76) },
            { 19, ("Wolf Pup", 35) }, { 20, ("Wind-up Warrior of Light", 77) }, { 21, ("Mammet #001", 2) },
            { 22, ("Cherry Bomb", 1) }, { 23, ("Wind-up Gentleman", 21) }, { 24, ("Wind-up Nanamo", 84) },
            { 25, ("Wayward Hatchling", 3) }, { 26, ("Wind-up Goblin", 49) }, { 27, ("Wind-up Gilgamesh", 85) },
            { 28, ("Slime Puddle", 47) }, { 29, ("Wind-up Ultros", 104) }, { 30, ("Bite-sized Pudding", 42) },
            { 31, ("Enkidu", 122) }, { 32, ("Pudgy Puk", 31) }, { 33, ("Baby Bun", 14) },
            { 34, ("Kidragora", 48) }, { 35, ("Coblyn Larva", 38) }, { 36, ("Chigoe Larva", 15) },
            { 37, ("Smallshell", 34) }, { 38, ("Demon Brick", 44) }, { 39, ("Infant Imp", 18) },
            { 40, ("Tight-beaked Parrot", 57) }, { 41, ("Mummy's Little Mummy", 112) }, { 42, ("Fat Cat", 110) },
            { 43, ("Baby Opo-opo", 80) }, { 44, ("Naughty Nanka", 102) }, { 45, ("Wind-up Louisoix", 118) },
            { 46, ("Gravel Golem", 22) }, { 47, ("Plush Cushion", 66) }, { 48, ("Tiny Rat", 13) },
            { 49, ("Bluebird", 16) }, { 50, ("Minute Mindflayer", 56) }, { 51, ("Cactuar Cutting", 33) },
            { 52, ("Baby Raptor", 25) }, { 53, ("Baby Bat", 26) }, { 54, ("Nutkin", 97) },
            { 55, ("Tiny Bulb", 27) }, { 56, ("Magic Broom", 81) }, { 57, ("Nana Bear", 95) },
            { 58, ("Model Vanguard", 43) }, { 59, ("Tiny Tortoise", 24) }, { 60, ("Wind-up Dullahan", 29) },
            { 61, ("Wind-up Tonberry", 23) }, { 62, ("Miniature Minecart", 96) }, { 63, ("Black Coeurl", 20) },
            { 64, ("Wind-up Aldgoat", 39) }, { 65, ("Wind-up Sun", 65) }, { 66, ("Tiny Tapir", 94) },
            { 67, ("Wind-up Qiqirn", 54) }, 
            { 68, ("Onion Prince", 86) }, { 69, ("Treasure Box", 93) }, { 70, ("Heavy Hatchling", 106) }
        };

        public CollectionWindow(Plugin plugin, IGameInteropProvider interopProvider) : base("My Collection###AetherialArenaCollectionWindow")
        {
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
                ImGui.TableSetupColumn("Needed to Unlock", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableHeadersRow();

                // Loop through every sprite in the game's data to show a complete list
                foreach (var sprite in dataManager.Sprites.OrderBy(s => s.ID))
                {
                    // Check the two unlock conditions separately
                    bool isCaptured = playerProfile.AttunedSpriteIDs.Contains(sprite.ID);
                    bool isMinionOwned = false;
                    string minionToUnlock = "N/A";

                    if (minionUnlockMap.TryGetValue(sprite.ID, out var minionData))
                    {
                        minionToUnlock = minionData.Name;
                        if (uiState != null)
                        {
                            isMinionOwned = uiState->IsCompanionUnlocked(minionData.Id);
                        }
                    }

                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);

                    // Rarity Indicator
                    var (rarityChar, rarityColor) = GetRarityDisplay(sprite.Rarity);
                    ImGui.TextColored(rarityColor, rarityChar);

                    ImGui.TableSetColumnIndex(1);

                    // Icon
                    var icon = assetManager.GetRecoloredIcon(sprite.IconName, sprite.RecolorKey);
                    if (icon != null)
                    {
                        // Icon is greyed out if the *minion* isn't owned
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
                    // This column now strictly shows the battle capture progress
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
                RarityTier.Uncommon => ("U", new Vector4(0.4f, 1.0f, 0.4f, 1.0f)), // Green
                RarityTier.Rare => ("R", new Vector4(0.5f, 0.5f, 1.0f, 1.0f)),    // Blue
                _ => ("C", new Vector4(0.8f, 0.8f, 0.8f, 1.0f)),                   // Grey
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

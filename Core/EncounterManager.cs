using AetherialArena.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Dalamud.Game.ClientState.Conditions;
using System.Threading.Tasks; // Added for audio

namespace AetherialArena.Core
{
    public class EncounterManager
    {
        private readonly Plugin plugin;
        private readonly Random random = new();

        public EncounterManager(Plugin p)
        {
            plugin = p;
        }

        public SearchResult SearchForEncounter(ushort? overrideTerritory = null, uint? overrideSubLocationId = null)
        {
            if (Plugin.ClientState.LocalPlayer == null || Plugin.Condition[ConditionFlag.InCombat] || Plugin.Condition[ConditionFlag.Mounted])
            {
                return SearchResult.InvalidState;
            }

            if (plugin.PlayerProfile.CurrentAether <= 0)
            {
                return SearchResult.NoAether;
            }

            plugin.PlayerProfile.CurrentAether--;
            plugin.SaveManager.SaveProfile(plugin.PlayerProfile);

            var currentTerritory = overrideTerritory ?? Plugin.ClientState.TerritoryType;
            var subLocationId = overrideSubLocationId ?? GetCurrentSubLocationId();
            var subLocationKey = subLocationId?.ToString();

            Plugin.Log.Info($"Searching for encounter in Territory: {currentTerritory}, Sub-Location ID: {subLocationKey ?? "None"}");

            var encounterData = plugin.DataManager.Encounters.FirstOrDefault(e => e.TerritoryTypeID == currentTerritory);
            if (encounterData == null || encounterData.EncountersBySubLocation == null)
            {
                Plugin.Log.Warning($"No encounter data found for territory {currentTerritory} in encountertables.json.");
                plugin.PlayerProfile.CurrentAether++; // Refund Aether
                return SearchResult.NoSpritesFound;
            }

            List<int>? availableSpriteIds = null;
            if (subLocationKey != null && encounterData.EncountersBySubLocation.TryGetValue(subLocationKey, out var subLocationSprites))
            {
                Plugin.Log.Info($"Found specific encounter list for Sub-Location ID: {subLocationKey}");
                availableSpriteIds = subLocationSprites;
            }
            else if (encounterData.EncountersBySubLocation.TryGetValue("Default", out var defaultSprites))
            {
                Plugin.Log.Info($"No specific sub-location list found. Using 'Default' list for territory {currentTerritory}.");
                availableSpriteIds = defaultSprites;
            }

            if (availableSpriteIds == null || !availableSpriteIds.Any())
            {
                Plugin.Log.Warning("Could not find a valid sprite list ('" + (subLocationKey ?? "Default") + "') for this location in encountertables.json.");
                plugin.PlayerProfile.CurrentAether++; // Refund Aether
                return SearchResult.NoSpritesFound;
            }

            var availableSprites = plugin.DataManager.Sprites.Where(s => availableSpriteIds.Contains(s.ID)).ToList();
            if (!availableSprites.Any())
            {
                Plugin.Log.Warning("Encounter list was found, but no matching sprites were loaded from sprites.json.");
                plugin.PlayerProfile.CurrentAether++; // Refund Aether
                return SearchResult.NoSpritesFound;
            }

            var weightedList = new List<Sprite>();
            foreach (var sprite in availableSprites)
            {
                int weight = GetRarityWeight(sprite.Rarity);
                for (int i = 0; i < weight; i++)
                {
                    weightedList.Add(sprite);
                }
            }

            if (!weightedList.Any())
            {
                Plugin.Log.Warning("Could not determine an encounter (weighted list was empty).");
                plugin.PlayerProfile.CurrentAether++; // Refund Aether
                return SearchResult.NoSpritesFound;
            }

            var opponentData = weightedList[random.Next(weightedList.Count)];

            // Added for audio
            plugin.AudioManager.PlaySfx("encounterfound.wav");
            // MODIFIED: Pass the currentTerritory to the StartBattle method
            plugin.BattleManager.StartBattle(plugin.PlayerProfile.Loadout, opponentData.ID, currentTerritory);

            plugin.HubWindow.IsOpen = false;
            plugin.MainWindow.IsOpen = true;
            return SearchResult.Success;

            
        }

        private int GetRarityWeight(RarityTier rarity)
        {
            return rarity switch
            {
                RarityTier.Uncommon => 5,
                RarityTier.Rare => 1,
                _ => 10,
            };
        }

        private unsafe uint? GetCurrentSubLocationId()
        {
            try
            {
                var territoryInfo = TerritoryInfo.Instance();
                if (territoryInfo == null) return null;
                var subAreaId = territoryInfo->SubAreaPlaceNameId;
                return subAreaId == 0 ? null : subAreaId;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

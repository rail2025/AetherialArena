using AetherialArena.Models;
using System;
using System.Linq;

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

        public void SearchForEncounter(ushort? overrideTerritory = null)
        {
            if (plugin.PlayerProfile.CurrentAether <= 0)
            {
                Plugin.Log.Info("Not enough Aether to search.");
                return;
            }

            plugin.PlayerProfile.CurrentAether--;
            plugin.SaveManager.SaveProfile(plugin.PlayerProfile);

            var currentTerritory = overrideTerritory ?? Plugin.ClientState.TerritoryType;
            var encounterData = plugin.DataManager.Encounters.FirstOrDefault(e => e.TerritoryTypeID == currentTerritory);

            if (encounterData == null || !encounterData.SpriteIDs.Any())
            {
                Plugin.Log.Info($"No encounters found for territory {currentTerritory}.");
                return;
            }

            var randomSpriteId = encounterData.SpriteIDs[random.Next(encounterData.SpriteIDs.Count)];
            var opponentData = plugin.DataManager.Sprites.FirstOrDefault(s => s.ID == randomSpriteId);

            if (opponentData == null)
            {
                Plugin.Log.Error($"Encounter table lists Sprite ID {randomSpriteId}, but it was not found.");
                return;
            }

            var playerData = plugin.DataManager.Sprites.FirstOrDefault();
            if (playerData == null)
            {
                Plugin.Log.Error("No player sprite available to start battle.");
                return;
            }

            plugin.BattleManager.StartBattle(new Sprite(playerData), new Sprite(opponentData));

            plugin.HubWindow.IsOpen = false;
            plugin.MainWindow.IsOpen = true;
        }
    }
}

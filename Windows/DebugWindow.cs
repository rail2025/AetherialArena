using System;
using System.Numerics;
using System.Linq;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using AetherialArena.Core;
using AetherialArena.Models;

namespace AetherialArena.Windows
{
    public class DebugWindow : Window, IDisposable
    {
        private readonly Plugin plugin;
        private int territoryIdAsInt = 129;
        private int specificSpriteId = 1;

        public DebugWindow(Plugin plugin) : base("Debug Tools###AetherialArenaDebugWindow")
        {
            this.plugin = plugin;
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(250, 150),
                MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
            };
        }

        public void Dispose() { }

        public override void Draw()
        {
            ImGui.Text("Encounter Testing");
            ImGui.Separator();

            if (ImGui.InputInt("Territory ID", ref territoryIdAsInt))
            {
                if (territoryIdAsInt < 0) territoryIdAsInt = 0;
                if (territoryIdAsInt > ushort.MaxValue) territoryIdAsInt = ushort.MaxValue;
            }

            if (ImGui.Button("Set Fake Location & Search"))
            {
                plugin.EncounterManager.SearchForEncounter((ushort)territoryIdAsInt);
            }

            ImGui.Spacing();
            ImGui.Text("Spawn Specific Encounter");
            ImGui.InputInt("Sprite ID", ref specificSpriteId);
            if (ImGui.Button("Start Battle with ID"))
            {
                var opponentData = plugin.DataManager.Sprites.FirstOrDefault(s => s.ID == specificSpriteId);
                var playerData = plugin.DataManager.Sprites.FirstOrDefault();

                if (playerData != null && opponentData != null)
                {
                    plugin.BattleManager.StartBattle(new Sprite(playerData), new Sprite(opponentData));
                }
                else
                {
                    Plugin.Log.Error($"Could not start debug battle. Player or Opponent ID {specificSpriteId} not found.");
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text("Player Profile Management");
            ImGui.Separator();

            if (ImGui.Button("Give 10 Aether"))
            {
                plugin.PlayerProfile.CurrentAether += 10;
                if (plugin.PlayerProfile.CurrentAether > plugin.PlayerProfile.MaxAether)
                {
                    plugin.PlayerProfile.CurrentAether = plugin.PlayerProfile.MaxAether;
                }
                plugin.SaveManager.SaveProfile(plugin.PlayerProfile);
            }

            // --- NEW: CLEAR COLLECTION BUTTON ---
            if (ImGui.Button("Clear Collection Data"))
            {
                plugin.PlayerProfile.AttunedSpriteIDs.Clear();
                plugin.PlayerProfile.DefeatCounts.Clear();
                plugin.PlayerProfile.MaxAether = 10;
                plugin.PlayerProfile.CurrentAether = 10;
                plugin.SaveManager.SaveProfile(plugin.PlayerProfile);
                Plugin.Log.Info("Player collection data has been cleared.");
            }
        }
    }
}

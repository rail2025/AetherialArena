using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;
using AetherialArena.Models;
using Dalamud.Interface.Windowing;

namespace AetherialArena.Windows
{
    public class DebugWindow : Window, IDisposable
    {
        private readonly Plugin plugin;
        private ushort territoryId = 0;
        private uint subLocationId = 0;
        private int specificSpriteId = 1;

        public DebugWindow(Plugin plugin) : base("Aetherial Arena Debug")
        {
            this.plugin = plugin;
        }

        public void Dispose() { }

        public override void Draw()
        {
            int tempTerritoryId = territoryId;
            if (ImGui.InputInt("Territory ID", ref tempTerritoryId))
            {
                territoryId = (ushort)Math.Clamp(tempTerritoryId, ushort.MinValue, ushort.MaxValue);
            }

            int tempSubLocationId = (int)subLocationId;
            if (ImGui.InputInt("Sub-Location ID", ref tempSubLocationId))
            {
                subLocationId = (uint)Math.Max(0, tempSubLocationId);
            }

            if (ImGui.Button("Set Fake Location & Search"))
            {
                plugin.QueueEncounterSearch(territoryId, subLocationId);
            }

            ImGui.Separator();

            ImGui.InputInt("Spawn Specific Encounter", ref specificSpriteId);
            if (ImGui.Button("Start Battle with ID"))
            {
                var opponentData = plugin.DataManager.GetSpriteData(specificSpriteId);
                if (opponentData != null)
                {
                    plugin.MainWindow.IsOpen = true;
                    plugin.BattleManager.StartBattle(plugin.PlayerProfile.Loadout, specificSpriteId);
                }
            }

            ImGui.Separator();
            ImGui.Text("Player Profile Management");
            if (ImGui.Button("Give 10 Aether"))
            {
                plugin.PlayerProfile.CurrentAether = Math.Min(plugin.PlayerProfile.MaxAether, plugin.PlayerProfile.CurrentAether + 10);
            }
            if (ImGui.Button("Clear Collection Data"))
            {
                plugin.PlayerProfile.AttunedSpriteIDs.Clear();
                plugin.PlayerProfile.DefeatCounts.Clear();
                plugin.PlayerProfile.AttunedSpriteIDs.AddRange(new[] { 1, 2, 3 });
            }
        }
    }
}

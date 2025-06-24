using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using AetherialArena.Core;

namespace AetherialArena.Windows
{
    public class DebugWindow : Window, IDisposable
    {
        private readonly Plugin plugin;
        private int territoryIdAsInt = 129;

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
            ImGui.Text("Offline Testing Tools");
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
            ImGui.Separator();
            ImGui.Spacing();

            // New button to give the player more Aether
            if (ImGui.Button("Give 10 Aether"))
            {
                plugin.PlayerProfile.CurrentAether += 10;
                plugin.SaveManager.SaveProfile(plugin.PlayerProfile);
            }
        }
    }
}

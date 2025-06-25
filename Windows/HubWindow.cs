using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace AetherialArena.Windows
{
    public class HubWindow : Window, IDisposable
    {
        private readonly Plugin plugin;

        public HubWindow(Plugin plugin) : base("Aetherial Arena Hub###AetherialArenaHubWindow")
        {
            this.plugin = plugin;
            this.Size = new Vector2(400, 300);
            this.SizeConstraints = new WindowSizeConstraints
            {
                MinimumSize = new Vector2(300, 200),
                MaximumSize = new Vector2(800, 600)
            };
        }

        public void Dispose() { }

        public override void Draw()
        {
            // --- Player Status ---
            var attunedCount = plugin.PlayerProfile.AttunedSpriteIDs.Count;
            ImGui.Text($"Player Level: {attunedCount}"); // Level is based on number of attuned sprites
            ImGui.SameLine(ImGui.GetWindowWidth() - 150);
            ImGui.Text($"Aether: {plugin.PlayerProfile.CurrentAether} / {plugin.PlayerProfile.MaxAether}");
            ImGui.Separator();
            ImGui.Spacing();

            // --- Arena Layout (Visual Placeholder) ---
            var contentSize = ImGui.GetContentRegionAvail();
            var arenaHeight = contentSize.Y - 80; // Reserve space for footer
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + arenaHeight / 4);
            ImGui.TextDisabled("The arena awaits...");

            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + arenaHeight / 4);
            var searchButtonSize = new Vector2(150, 0);
            ImGui.SetCursorPosX((ImGui.GetWindowWidth() - searchButtonSize.X) / 2);
            if (ImGui.Button("Search for Sprites", searchButtonSize))
            {
                plugin.EncounterManager.SearchForEncounter();
            }

            // --- Footer Buttons ---
            ImGui.SetCursorPosY(ImGui.GetWindowHeight() - 35);
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.Button("View Collection"))
            {
                plugin.CollectionWindow.Toggle();
            }
            ImGui.SameLine();
            if (ImGui.Button("Settings"))
            {
                plugin.ConfigWindow.Toggle();
            }
            ImGui.SameLine();
            if (ImGui.Button("Main Menu"))
            {
                this.IsOpen = false;
                plugin.TitleWindow.IsOpen = true;
            }
        }
    }
}

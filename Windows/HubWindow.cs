using System;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace AetherialArena.Windows
{
    public class HubWindow : Window, IDisposable
    {
        private readonly Plugin plugin;
        private string? statusMessage;
        private bool isShowingStatus;

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

        public void SetStatusMessage(string message)
        {
            statusMessage = message;
            isShowingStatus = true;
            Task.Delay(3000).ContinueWith(_ => isShowingStatus = false);
        }

        public override void Draw()
        {
            var attunedCount = plugin.PlayerProfile.AttunedSpriteIDs.Count;
            ImGui.Text($"Player Level: {attunedCount}");
            ImGui.SameLine(ImGui.GetWindowWidth() - 150);
            ImGui.Text($"Aether: {plugin.PlayerProfile.CurrentAether} / {plugin.PlayerProfile.MaxAether}");
            ImGui.Separator();
            ImGui.Spacing();

            var contentSize = ImGui.GetContentRegionAvail();
            var arenaHeight = contentSize.Y - 80;
            var centerPos = ImGui.GetCursorPosY() + arenaHeight / 2;

            if (isShowingStatus && !string.IsNullOrEmpty(statusMessage))
            {
                var textSize = ImGui.CalcTextSize(statusMessage);
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - textSize.X) / 2);
                ImGui.SetCursorPosY(centerPos - 30);
                ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.8f, 1.0f), statusMessage);
            }
            else
            {
                ImGui.SetCursorPosY(centerPos - 30);
                ImGui.TextDisabled("The arena awaits...");
            }

            var searchButtonSize = new Vector2(150, 0);
            ImGui.SetCursorPosX((ImGui.GetWindowWidth() - searchButtonSize.X) / 2);
            ImGui.SetCursorPosY(centerPos);

            if (ImGui.Button("Search for Sprites", searchButtonSize))
            {
                plugin.QueueEncounterSearch();
            }

            ImGui.SetCursorPosY(ImGui.GetWindowHeight() - 35);
            ImGui.Separator();
            ImGui.Spacing();

            // Changed to open the Codex window
            if (ImGui.Button("View Codex")) { plugin.CodexWindow.Toggle(); }
            ImGui.SameLine();
            if (ImGui.Button("Settings")) { plugin.ConfigWindow.Toggle(); }
            ImGui.SameLine();
            if (ImGui.Button("Main Menu"))
            {
                this.IsOpen = false;
                plugin.TitleWindow.IsOpen = true;
            }
        }
    }
}

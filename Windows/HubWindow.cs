using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using AetherialArena.Models;
using AetherialArena.Services;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace AetherialArena.Windows
{
    public class HubWindow : Window
    {
        private enum HubState
        {
            Default,
            ManagingLoadout,
            ChoosingSprite
        }

        private readonly Plugin plugin;
        private readonly AssetManager assetManager;
        private readonly DataManager dataManager;
        private string statusMessage = string.Empty;

        private HubState currentState = HubState.Default;
        private int slotToEdit = -1;

        public HubWindow(Plugin plugin) : base("Aetherial Arena Hub###AetherialArenaHubWindow")
        {
            this.plugin = plugin;
            this.assetManager = plugin.AssetManager;
            this.dataManager = plugin.DataManager;

            this.Size = new Vector2(300, 400);
            this.Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar;
        }

        public override void PreDraw()
        {
            // Reset state if the window is closed and re-opened
            if (!IsOpen)
            {
                currentState = HubState.Default;
                slotToEdit = -1;
            }
        }

        public void SetStatusMessage(string message)
        {
            this.statusMessage = message;
        }

        public override void Draw()
        {
            ImGui.Text($"Player Level: {plugin.PlayerProfile.AttunedSpriteIDs.Count}");
            ImGui.SameLine();
            ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 100);
            ImGui.Text($"Aether: {plugin.PlayerProfile.CurrentAether} / {plugin.PlayerProfile.MaxAether}");
            ImGui.Separator();
            ImGui.Spacing();

            switch (currentState)
            {
                case HubState.Default:
                    DrawDefaultView();
                    break;
                case HubState.ManagingLoadout:
                    DrawLoadoutManagementView();
                    break;
                case HubState.ChoosingSprite:
                    DrawSpriteChooserView();
                    break;
            }
        }

        private void DrawDefaultView()
        {
            DrawLoadoutDisplay();

            ImGui.Separator();
            ImGui.Spacing();

            var buttonSize = new Vector2(ImGui.GetContentRegionAvail().X, 0);

            if (ImGui.Button("Change Loadout", buttonSize))
            {
                currentState = HubState.ManagingLoadout;
            }
            if (ImGui.Button("Search for Sprite", buttonSize))
            {
                plugin.AudioManager.PlaySfx("menuselect.wav");
                Task.Run(async () => {
                    await plugin.AudioManager.StopMusic(1.0f);
                    plugin.AudioManager.PlaySfx("encountersearch.wav"); 
                    await Task.Delay(1500); // Wait for the sfx to play
                    plugin.QueueEncounterSearch();
                });
            }

            ImGui.Spacing();
            if (ImGui.Button("Codex", buttonSize)) plugin.CodexWindow.Toggle();
            if (ImGui.Button("Collection", buttonSize)) plugin.CollectionWindow.Toggle();
            if (ImGui.Button("Settings", buttonSize)) plugin.ConfigWindow.Toggle();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (!string.IsNullOrEmpty(statusMessage))
            {
                var textSize = ImGui.CalcTextSize(statusMessage);
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - textSize.X) / 2);
                ImGui.Text(statusMessage);
            }
        }

        private void DrawLoadoutDisplay()
        {
            ImGui.Text("Current Loadout:");
            ImGui.Indent();
            for (int i = 0; i < plugin.PlayerProfile.Loadout.Count; i++)
            {
                var spriteId = plugin.PlayerProfile.Loadout[i];
                var sprite = dataManager.GetSpriteData(spriteId);
                if (sprite != null)
                {
                    var icon = assetManager.GetRecoloredIcon(sprite.IconName, sprite.RecolorKey);
                    if (icon != null) ImGui.Image(icon.ImGuiHandle, new Vector2(64, 64));
                    else ImGui.Dummy(new Vector2(64, 64));

                    if (i < plugin.PlayerProfile.Loadout.Count - 1) ImGui.SameLine();
                }
            }
            ImGui.Unindent();
        }

        private void DrawLoadoutManagementView()
        {
            ImGui.Text("Manage Your Loadout");
            ImGui.Separator();

            for (int i = 0; i < 3; i++)
            {
                var spriteId = plugin.PlayerProfile.Loadout[i];
                var sprite = dataManager.GetSpriteData(spriteId);
                if (sprite != null)
                {
                    var icon = assetManager.GetRecoloredIcon(sprite.IconName, sprite.RecolorKey);
                    if (icon != null) ImGui.Image(icon.ImGuiHandle, new Vector2(40, 40));
                    else ImGui.Dummy(new Vector2(40, 40));

                    ImGui.SameLine();
                    ImGui.Text(sprite.Name);
                    ImGui.SameLine();
                    ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 80);
                    if (ImGui.Button($"Change##{i}"))
                    {
                        slotToEdit = i;
                        currentState = HubState.ChoosingSprite;
                    }
                }
                ImGui.Separator();
            }

            if (ImGui.Button("Back", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
            {
                currentState = HubState.Default;
            }
        }

        private void DrawSpriteChooserView()
        {
            ImGui.Text($"Choose a Sprite for Slot {slotToEdit + 1}");
            ImGui.Separator();

            ImGui.BeginChild("SpriteChooser", new Vector2(0, ImGui.GetContentRegionAvail().Y - 40), true);
            foreach (var attunedId in plugin.PlayerProfile.AttunedSpriteIDs)
            {
                var sprite = dataManager.GetSpriteData(attunedId);
                if (sprite == null) continue;

                bool isAlreadyInLoadout = plugin.PlayerProfile.Loadout.Contains(attunedId);
                if (isAlreadyInLoadout) ImGui.BeginDisabled();

                if (ImGui.Selectable($"{sprite.Name}##{attunedId}"))
                {
                    plugin.PlayerProfile.Loadout[slotToEdit] = attunedId;
                    plugin.SaveManager.SaveProfile(plugin.PlayerProfile);
                    currentState = HubState.ManagingLoadout;
                }

                if (isAlreadyInLoadout) ImGui.EndDisabled();
            }
            ImGui.EndChild();

            ImGui.Separator();
            if (ImGui.Button("Back", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
            {
                currentState = HubState.ManagingLoadout;
            }
        }
    }
}

using System;
using System.Linq;
using System.Numerics;
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
            this.Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar;
        }

        public override void OnClose()
        {
            // MODIFIED: Call StopMusic() with no arguments, and no Task.Run is needed.
            plugin.AudioManager.StopMusic();
        }
        public override void PreDraw()
        {
            var baseSize = new Vector2(300, 400);
            this.Size = baseSize * plugin.Configuration.CustomUiScale;
            Flags = plugin.Configuration.ShowDalamudTitleBars ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoTitleBar;
            Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar;

            if (plugin.Configuration.LockAllWindows)
            {
                Flags |= ImGuiWindowFlags.NoMove;
            }
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

        private void DrawTextWithOutline(string text, Vector2 pos, uint textColor, uint outlineColor)
        {
            var drawList = ImGui.GetWindowDrawList();
            var outlineOffset = new Vector2(1, 1);
            drawList.AddText(pos - outlineOffset, outlineColor, text);
            drawList.AddText(pos + new Vector2(outlineOffset.X, -outlineOffset.Y), outlineColor, text);
            drawList.AddText(pos + new Vector2(-outlineOffset.X, outlineOffset.Y), outlineColor, text);
            drawList.AddText(pos + outlineOffset, outlineColor, text);
            drawList.AddText(pos, textColor, text);
        }

        private bool DrawButtonWithOutline(string id, string text, Vector2 size)
        {
            var clicked = ImGui.Button($"##{id}", size);
            if (clicked)
            {
                plugin.AudioManager.PlaySfx("menuselect.wav");
            }
            var buttonPos = ImGui.GetItemRectMin();
            var buttonSize = ImGui.GetItemRectSize();
            var textSize = ImGui.CalcTextSize(text);
            var textPos = buttonPos + (buttonSize - textSize) * 0.5f;

            DrawTextWithOutline(text, textPos, 0xFFFFFFFF, 0xFF000000);

            return clicked;
        }

        private void DrawOutlinedText(string text)
        {
            var cursorPos = ImGui.GetCursorScreenPos();
            DrawTextWithOutline(text, cursorPos, 0xFFFFFFFF, 0xFF000000);
            ImGui.Dummy(ImGui.CalcTextSize(text));
        }

        public override void Draw()
        {
            var backgroundTexture = this.assetManager.GetIcon("hubbackground.png");
            if (backgroundTexture != null)
            {
                var windowPos = ImGui.GetWindowPos();
                var windowSize = ImGui.GetWindowSize();
                ImGui.GetWindowDrawList().AddImage(backgroundTexture.ImGuiHandle, windowPos, windowPos + windowSize);
            }
            ImGui.Text($"Player Level: {plugin.PlayerProfile.AttunedSpriteIDs.Count}");
            ImGui.SameLine();

            var currentAether = plugin.PlayerProfile.CurrentAether;
            var maxAether = plugin.PlayerProfile.MaxAether;
            var fraction = maxAether > 0 ? (float)currentAether / maxAether : 0f;
            var overlay = $"{currentAether}/{maxAether}";
            var label = "Aether:";

            var barWidth = 120f;
            var labelWidth = ImGui.CalcTextSize(label).X;
            var spacing = ImGui.GetStyle().ItemSpacing.X;
            var groupWidth = labelWidth + spacing + barWidth;

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - groupWidth);

            ImGui.Text(label);
            ImGui.SameLine();

            ImGui.PushItemWidth(barWidth);
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(0.6f, 0.4f, 1.0f, 1.0f));
            ImGui.ProgressBar(fraction, new Vector2(0, 0), overlay);
            ImGui.PopStyleColor();
            ImGui.PopItemWidth();

            ImGui.Separator();

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

            ImGui.SetCursorPosY(ImGui.GetWindowHeight() * 0.5f);

            ImGui.Spacing();

            var buttonSize = new Vector2(ImGui.GetContentRegionAvail().X, 0);

            if (DrawButtonWithOutline("ChangeLoadoutButton", "Change Loadout", buttonSize))
            {
                currentState = HubState.ManagingLoadout;
            }
            if (DrawButtonWithOutline("SearchForSpriteButton", "Search for Sprite", buttonSize))
            {
                plugin.AudioManager.PlaySfx("encountersearch.wav");
                plugin.QueueEncounterSearch();
            }

            ImGui.Spacing();
            if (plugin.PlayerProfile.AttunedSpriteIDs.Count >= 70)
            {
                if (ImGui.Button("Enter the Aetherial Arena", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
                {
                    plugin.ArenaSelectionWindow.IsOpen = true;
                }
            }

            if (DrawButtonWithOutline("CodexButton", "Codex", buttonSize)) plugin.CodexWindow.Toggle();
            if (DrawButtonWithOutline("CollectionButton", "Collection", buttonSize)) plugin.CollectionWindow.Toggle();
            if (DrawButtonWithOutline("SettingsButton", "Settings", buttonSize)) plugin.ConfigWindow.Toggle();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (!string.IsNullOrEmpty(statusMessage))
            {
                var textSize = ImGui.CalcTextSize(statusMessage);
                var textPos = new Vector2(ImGui.GetCursorScreenPos().X + (ImGui.GetContentRegionAvail().X - textSize.X) / 2, ImGui.GetCursorScreenPos().Y);
                DrawTextWithOutline(statusMessage, textPos, 0xFFFFFFFF, 0xFF000000);
                ImGui.Dummy(textSize);
            }
        }

        private void DrawLoadoutDisplay()
        {
            DrawOutlinedText("Current Loadout:");

            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 30);

            ImGui.Indent();

            var iconSize = new Vector2(77, 77);

            for (int i = 0; i < plugin.PlayerProfile.Loadout.Count; i++)
            {
                var spriteId = plugin.PlayerProfile.Loadout[i];
                var sprite = dataManager.GetSpriteData(spriteId);
                if (sprite != null)
                {
                    var icon = assetManager.GetRecoloredIcon(sprite.IconName, sprite.RecolorKey);
                    if (icon != null) ImGui.Image(icon.ImGuiHandle, iconSize);
                    else ImGui.Dummy(iconSize);

                    if (i < plugin.PlayerProfile.Loadout.Count - 1) ImGui.SameLine();
                }
            }
            ImGui.Unindent();
        }

        private void DrawLoadoutManagementView()
        {
            DrawOutlinedText("Manage Your Loadout");
            ImGui.Separator();
            ImGui.SetCursorPosY(ImGui.GetWindowHeight() * 0.5f);
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
                    DrawOutlinedText(sprite.Name);

                    ImGui.SameLine();
                    ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 80);
                    if (DrawButtonWithOutline($"ChangeButton##{i}", "Change", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
                    {
                        slotToEdit = i;
                        currentState = HubState.ChoosingSprite;
                    }
                }
                ImGui.Separator();
            }

            if (DrawButtonWithOutline("BackButton", "Back", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
            {
                currentState = HubState.Default;
            }
        }

        private void DrawSpriteChooserView()
        {
            DrawOutlinedText($"Choose a Sprite for Slot {slotToEdit + 1}");
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
            if (DrawButtonWithOutline("BackChooserButton", "Back", new Vector2(ImGui.GetContentRegionAvail().X, 0)))
            {
                currentState = HubState.ManagingLoadout;
            }
        }
    }
}

using System;
using System.Numerics;
using System.Threading.Tasks;
using AetherialArena.Services;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace AetherialArena.Windows
{
    public class TitleWindow : Window, IDisposable
    {
        private readonly Plugin plugin;
        private readonly AssetManager assetManager;

        public TitleWindow(Plugin plugin) : base("Aetherial Arena###AetherialArenaTitleWindow")
        {
            this.plugin = plugin;
            this.assetManager = plugin.AssetManager;

            this.Size = new Vector2(600, 425);
            this.SizeCondition = ImGuiCond.FirstUseEver;

            this.Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar;
        }

        public override void OnOpen()
        {
            plugin.AudioManager.PlayMusic("titlemusic.mp3", true, 1.0f);
        }

        public override void OnClose()
        {
            // Stop music only if the main battle window isn't opening
            if (!plugin.MainWindow.IsOpen)
            {
                Task.Run(() => plugin.AudioManager.StopMusic(1.0f));
            }
        }


        public void Dispose() { }

        public override void Draw()
        {
            var backgroundTexture = this.assetManager.GetIcon("icon.png");

            ImGui.PushStyleColor(ImGuiCol.WindowBg, 0);
            if (backgroundTexture != null)
            {
                ImGui.SetWindowSize(new Vector2(backgroundTexture.Width, backgroundTexture.Height));

                var windowPos = ImGui.GetWindowPos();
                var windowSize = ImGui.GetWindowSize();
                ImGui.GetWindowDrawList().AddImage(backgroundTexture.ImGuiHandle, windowPos, windowPos + windowSize);
            }

            var windowHeight = ImGui.GetWindowHeight();
            var windowWidth = ImGui.GetWindowWidth();
            var padding = 15f;
            var itemSpacing = 8f;

            var buttonHeight = ImGui.CalcTextSize("A").Y + ImGui.GetStyle().FramePadding.Y * 2;
            var leftColumnContentHeight = (buttonHeight * 3) + (itemSpacing * 2);
            var leftColumnY = windowHeight - leftColumnContentHeight - padding;

            ImGui.SetCursorPos(new Vector2(padding, leftColumnY));

            ImGui.BeginGroup();

            var buttonSize = new Vector2(windowWidth * 0.4f, 0);

            if (DrawButtonWithOutline("EnterArena", "Enter the Arena", buttonSize))
            {
                this.IsOpen = false;
                plugin.HubWindow.IsOpen = true;
            }

            ImGui.Spacing();

            if (DrawButtonWithOutline("Collection", "View Codex", buttonSize))
            {
                plugin.CodexWindow.Toggle();
            }

            ImGui.Spacing();

            if (DrawButtonWithOutline("About", "About", buttonSize))
            {
                plugin.AboutWindow.Toggle();
            }

            ImGui.EndGroup();

            var checkboxHeight = ImGui.GetFrameHeight();
            var rightColumnContentHeight = buttonHeight + (checkboxHeight * 2) + (itemSpacing * 2);
            var rightColumnY = windowHeight - rightColumnContentHeight - padding;

            ImGui.SetCursorPos(new Vector2(windowWidth - buttonSize.X - padding, rightColumnY));

            ImGui.BeginGroup();

            if (DrawButtonWithOutline("Settings", "Settings", buttonSize))
            {
                plugin.ConfigWindow.Toggle();
            }

            ImGui.Spacing();

            bool musicMuted = plugin.Configuration.IsBgmMuted;
            if (DrawCheckboxWithOutline("MuteMusic", "Mute Music", ref musicMuted))
            {
                plugin.Configuration.IsBgmMuted = musicMuted;
                plugin.Configuration.Save();
            }

            ImGui.Spacing();

            bool sfxMuted = plugin.Configuration.IsSfxMuted;
            if (DrawCheckboxWithOutline("MuteSFX", "Mute SFX", ref sfxMuted))
            {
                plugin.Configuration.IsSfxMuted = sfxMuted;
                plugin.Configuration.Save();
            }

            ImGui.EndGroup();

            ImGui.PopStyleColor();
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

        private bool DrawCheckboxWithOutline(string id, string text, ref bool isChecked)
        {
            var startPos = ImGui.GetCursorScreenPos();

            bool clicked = ImGui.Checkbox($"##{id}", ref isChecked);
            if (clicked)
            {
                plugin.AudioManager.PlaySfx("menuselect.wav");
            }

            ImGui.SameLine();

            var labelPos = ImGui.GetCursorScreenPos();
            labelPos.Y = startPos.Y + (ImGui.GetFrameHeight() - ImGui.CalcTextSize(text).Y) / 2;
            DrawTextWithOutline(text, labelPos, 0xFFFFFFFF, 0xFF000000);

            ImGui.Dummy(new Vector2(0, ImGui.GetFrameHeight()));

            return clicked;
        }
    }
}

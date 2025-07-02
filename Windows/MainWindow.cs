using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using AetherialArena.Core;
using AetherialArena.UI;
using ImGuiNET;
using AetherialArena.Services;
using System.Threading.Tasks;

namespace AetherialArena.Windows
{
    public class MainWindow : Window, IDisposable
    {
        private readonly BattleManager battleManager;
        private readonly BattleUIComponent battleUIComponent;
        private readonly AssetManager assetManager;
        private readonly Plugin plugin;

        public MainWindow(Plugin plugin, BattleUIComponent battleUIComponent) : base("Aetherial Arena Battle")
        {
            this.plugin = plugin;
            this.battleManager = plugin.BattleManager;
            this.battleUIComponent = battleUIComponent;
            this.assetManager = plugin.AssetManager;
            this.SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(600, 400), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
            this.IsOpen = false;
            this.Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar;
        }

        public void Dispose() { }

        public override void OnOpen()
        {
            plugin.AudioManager.PlayMusic("fightmusic.mp3", true, 0.5f);
        }

        public override void OnClose()
        {
        }
        public override void PreDraw()
        {
            // Use the MinimumSize as the base
            var baseSize = new Vector2(640, 540);
            this.Size = baseSize * plugin.Configuration.CustomUiScale;

            // Keep original flags to ensure it's non-resizable
            this.Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar;
        }

        public override void Draw()
        {
            var background = assetManager.GetIcon(battleManager.CurrentBackgroundName);
            if (background != null)
            {
                var windowPos = ImGui.GetWindowPos();
                var windowSize = ImGui.GetWindowSize();
                ImGui.GetWindowDrawList().AddImage(background.ImGuiHandle, windowPos, windowPos + windowSize);
            }

            battleManager.Update();
            battleUIComponent.Update();

            switch (battleManager.State)
            {
                case BattleManager.BattleState.InProgress:
                    battleUIComponent.Draw();
                    break;
                case BattleManager.BattleState.PlayerVictory:
                    DrawEndScreen("You win!");
                    break;
                case BattleManager.BattleState.OpponentVictory:
                    DrawEndScreen("You lose!");
                    break;
            }
        }

        // Helper method to draw outlined text, now local to this window
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

        private void DrawEndScreen(string message)
        {
            var windowSize = ImGui.GetWindowSize();
            var textSize = ImGui.CalcTextSize(message);
            ImGui.SetCursorPosX((windowSize.X - textSize.X) * 0.5f);
            ImGui.SetCursorPosY(windowSize.Y / 3);
            ImGui.Text(message);

            // --- MODIFIED: Draw the specific unlock message instead of the whole log ---
            if (!string.IsNullOrEmpty(battleManager.UnlockMessage))
            {
                ImGui.Spacing();
                ImGui.SetWindowFontScale(1.2f); // Make font larger

                var unlockMessage = battleManager.UnlockMessage;
                var messageSize = ImGui.CalcTextSize(unlockMessage);

                // Center the text, accounting for the font scale
                var cursorPosX = (windowSize.X - (messageSize.X * 1.2f)) / 2;
                ImGui.SetCursorPosX(cursorPosX);

                // Draw with yellow color and black outline
                DrawTextWithOutline(unlockMessage, ImGui.GetCursorScreenPos(), 0xFF00FFFF, 0xFF000000);

                //ImGui.Dummy(messageSize * 1.2f);

                ImGui.SetWindowFontScale(1.0f); // Reset font scale to normal
            }

            var buttonText = "Return to Hub";
            var buttonTextSize = ImGui.CalcTextSize(buttonText) + ImGui.GetStyle().FramePadding * 2;
            ImGui.SetCursorPosX((windowSize.X - buttonTextSize.X) * 0.5f);
            ImGui.SetCursorPosY(windowSize.Y / 2);

            if (ImGui.Button(buttonText))
            {
                plugin.AudioManager.PlaySfx("menuselect.wav");
                Task.Run(async () => {
                    await plugin.AudioManager.StopMusic(0.25f);
                    plugin.AudioManager.PlayMusic("titlemusic.mp3", true, 1.0f);
                });
                battleManager.EndBattle();
            }
        }
    }
}

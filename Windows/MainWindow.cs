using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using AetherialArena.Core;
using AetherialArena.UI;
using ImGuiNET;
using AetherialArena.Services;
using System.Threading.Tasks; // Added for audio

namespace AetherialArena.Windows
{
    public class MainWindow : Window, IDisposable
    {
        private readonly BattleManager battleManager;
        private readonly BattleUIComponent battleUIComponent;
        private readonly AssetManager assetManager;
        private readonly Plugin plugin; // Added for audio

        public MainWindow(Plugin plugin, BattleUIComponent battleUIComponent) : base("Aetherial Arena Battle")
        {
            this.plugin = plugin; // Added for audio
            this.battleManager = plugin.BattleManager;
            this.battleUIComponent = battleUIComponent;
            this.assetManager = plugin.AssetManager;
            this.SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(600, 400), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
            this.IsOpen = false;
            this.Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar;
        }

        public override void PreDraw()
        {
            Flags = plugin.Configuration.ShowDalamudTitleBars ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoTitleBar;
            Flags |= ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar;

            if (plugin.Configuration.LockAllWindows)
            {
                Flags |= ImGuiWindowFlags.NoMove;
            }
        }

        public void Dispose() { }

        // Added for audio
        public override void OnOpen()
        {
            plugin.AudioManager.PlayMusic("fightmusic.mp3", true, 0.5f);
        }

        // Added for audio
        public override void OnClose()
        {
            Task.Run(() => plugin.AudioManager.StopMusic(0.5f));
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
            battleUIComponent.Update();
            battleManager.Update();

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


        private void DrawEndScreen(string message)
        {
            var windowSize = ImGui.GetWindowSize();
            var textSize = ImGui.CalcTextSize(message);
            ImGui.SetCursorPosX((windowSize.X - textSize.X) * 0.5f);
            ImGui.SetCursorPosY(windowSize.Y / 3);
            ImGui.Text(message);

            var buttonText = "Return to Hub";
            var buttonTextSize = ImGui.CalcTextSize(buttonText) + ImGui.GetStyle().FramePadding * 2;
            ImGui.SetCursorPosX((windowSize.X - buttonTextSize.X) * 0.5f);
            ImGui.SetCursorPosY(windowSize.Y / 2);

            if (ImGui.Button(buttonText))
            {
                plugin.AudioManager.PlaySfx("menuselect.wav");
                // Stop battle music and play title music
                Task.Run(async () => {
                    await plugin.AudioManager.StopMusic(0.25f);
                    plugin.AudioManager.PlayMusic("titlemusic.mp3", true, 1.0f);
                });
                battleManager.EndBattle();
            }
        }
    }
}

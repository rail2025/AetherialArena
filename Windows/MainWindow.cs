using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using AetherialArena.Core;
using AetherialArena.UI;
using Dalamud.Bindings.ImGui;
using AetherialArena.Services;

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
            if (battleManager.IsBossBattle)
            {
                string bossMusic = $"bossmusic{battleManager.CurrentOpponentId - 70}.mp3";
                plugin.AudioManager.PlayMusic(bossMusic, true);
            }
            else
            {
                plugin.AudioManager.StartBattlePlaylist();
            }
        }

        public override void OnClose() { }

        public override void PreDraw()
        {
            var baseSize = new Vector2(640, 540);
            this.Size = baseSize * plugin.Configuration.CustomUiScale;
            this.Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar;
        }

        public override void Draw()
        {
            var background = assetManager.GetIcon(battleManager.CurrentBackgroundName);
            if (background != null)
            {
                var windowPos = ImGui.GetWindowPos();
                var windowSize = ImGui.GetWindowSize();
                ImGui.GetWindowDrawList().AddImage(background.Handle, windowPos, windowPos + windowSize);
            }

            battleManager.Update();
            battleUIComponent.Update();


            if (battleManager.ShouldRollCredits)
            {
                if (this.IsOpen)
                {
                    this.IsOpen = false;
                    plugin.CreditsWindow.IsOpen = true;
                }
                return;
            }


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

            if (battleManager.AllSpritesCaptured)
            {
                var background = assetManager.GetIcon("grass.png");
                if (background != null)
                {
                    var windowPos = ImGui.GetWindowPos();
                    ImGui.GetWindowDrawList().AddImage(background.Handle, windowPos, windowPos + windowSize);
                }

                string line1 = "Congratulations! In case you forgot, here's what outside looks like:";
                string line2 = "Arena unlocked!";

                var line1Size = ImGui.CalcTextSize(line1);
                ImGui.SetCursorPosX((windowSize.X - line1Size.X) * 0.5f);
                ImGui.SetCursorPosY(windowSize.Y * 0.2f);
                DrawTextWithOutline(line1, ImGui.GetCursorScreenPos(), 0xFFFFFFFF, 0xFF000000);

                ImGui.Dummy(new Vector2(0, line1Size.Y));

                var line2Size = ImGui.CalcTextSize(line2);
                ImGui.SetCursorPosX((windowSize.X - line2Size.X) * 0.5f);
                DrawTextWithOutline(line2, ImGui.GetCursorScreenPos(), 0xFFFFFFFF, 0xFF000000);

            }
            else
            {
                var textSize = ImGui.CalcTextSize(message);
                ImGui.SetCursorPosX((windowSize.X - textSize.X) * 0.5f);
                ImGui.SetCursorPosY(windowSize.Y * 0.15f);
                ImGui.Text(message);

                if (battleManager.VictoryMessages.Any())
                {
                    var childSize = new Vector2(windowSize.X * 0.8f, 120);
                    ImGui.SetCursorPosX((windowSize.X - childSize.X) * 0.5f);
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 20);

                    ImGui.BeginChild("VictoryLog", childSize, true);
                    foreach (var victoryMessage in battleManager.VictoryMessages)
                    {
                        ImGui.TextWrapped(victoryMessage);
                    }
                    ImGui.EndChild();
                }

                if (!string.IsNullOrEmpty(battleManager.UnlockMessage))
                {
                    ImGui.Spacing();
                    ImGui.SetWindowFontScale(1.2f);
                    var unlockMessage = battleManager.UnlockMessage;
                    var messageSize = ImGui.CalcTextSize(unlockMessage);
                    var cursorPosX = (windowSize.X - (messageSize.X * 1.2f)) / 2;
                    ImGui.SetCursorPosX(cursorPosX);
                    DrawTextWithOutline(unlockMessage, ImGui.GetCursorScreenPos(), 0xFF00FFFF, 0xFF000000);
                    ImGui.SetWindowFontScale(1.0f);
                }
            }

            var buttonText = "Return to Hub";
            var buttonTextSize = ImGui.CalcTextSize(buttonText) + ImGui.GetStyle().FramePadding * 2;
            ImGui.SetCursorPosX((windowSize.X - buttonTextSize.X) * 0.5f);
            ImGui.SetCursorPosY(windowSize.Y * 0.75f);

            if (ImGui.Button(buttonText, new Vector2(buttonTextSize.X, 0)))
            {
                plugin.AudioManager.PlaySfx("menuselect.wav");
                plugin.AudioManager.StopMusic();
                plugin.AudioManager.PlayMusic("titlemusic.mp3", true);
                battleManager.EndBattle();
            }
        }
    }
}

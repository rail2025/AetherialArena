using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using AetherialArena.Core;
using AetherialArena.UI;
using ImGuiNET;

namespace AetherialArena.Windows
{
    public class MainWindow : Window, IDisposable
    {
        private readonly BattleManager battleManager;
        private readonly BattleUIComponent battleUIComponent;

        public MainWindow(Plugin plugin, BattleUIComponent battleUIComponent) : base("Aetherial Arena Battle")
        {
            this.battleManager = plugin.BattleManager;
            this.battleUIComponent = battleUIComponent;
            this.SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(600, 400), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };
            this.IsOpen = false;
        }

        public void Dispose() { }

        public override void Draw()
        {
            battleManager.Update();

            // This switch statement is re-introduced to handle the end-of-battle screens.
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
                battleManager.EndBattle();
            }
        }
    }
}

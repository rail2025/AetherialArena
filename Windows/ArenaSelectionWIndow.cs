using Dalamud.Bindings.ImGui;
using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using AetherialArena.Models;

namespace AetherialArena.Windows
{
    public class ArenaSelectionWindow : Window, IDisposable
    {
        private readonly Plugin plugin;

        public ArenaSelectionWindow(Plugin plugin) : base("Aetherial Arena")
        {
            this.plugin = plugin;
            this.Size = new Vector2(350, 400);
            this.Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar;
        }

        public void Dispose() { }

        public override void Draw()
        {
            ImGui.Text("Choose your opponent:");
            ImGui.Separator();

            var bosses = plugin.DataManager.Sprites.Where(s => s.Rarity == RarityTier.Boss).ToList();
            var elementalLords = bosses.Where(b => b.ID >= 71 && b.ID <= 76).ToList();
            var finalBoss = bosses.FirstOrDefault(b => b.ID == 77);

            foreach (var boss in elementalLords)
            {
                if (ImGui.Button(boss.Name, new Vector2(-1, 0)))
                {
                    plugin.BattleManager.StartArenaBattle(boss.ID);
                    plugin.MainWindow.IsOpen = true;
                    this.IsOpen = false;
                }
            }

            ImGui.Separator();

            int defeatedCount = plugin.PlayerProfile.DefeatedArenaBosses.Count(id => id >= 71 && id <= 76);
            bool finalBossUnlocked = defeatedCount >= 6;

            if (!finalBossUnlocked)
            {
                ImGui.Text($"Defeat the 6 Elemental Lords to unlock the final challenge. ({defeatedCount}/6)");
                ImGui.BeginDisabled();
            }

            if (finalBoss != null)
            {
                if (ImGui.Button(finalBoss.Name, new Vector2(-1, 0)))
                {
                    plugin.BattleManager.StartArenaBattle(finalBoss.ID);
                    plugin.MainWindow.IsOpen = true;
                    this.IsOpen = false;
                }
            }

            if (!finalBossUnlocked)
            {
                ImGui.EndDisabled();
            }
        }
    }
}

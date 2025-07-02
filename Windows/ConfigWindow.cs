using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace AetherialArena.Windows
{
    public class ConfigWindow : Window, IDisposable
    {
        private readonly Plugin plugin;
        private readonly Configuration configuration;

        public ConfigWindow(Plugin plugin) : base("Aetherial Arena - Settings")
        {
            this.Size = new Vector2(300, 250); // Adjusted size for the new slider
            this.SizeCondition = ImGuiCond.FirstUseEver;

            this.plugin = plugin;
            this.configuration = plugin.Configuration;
        }

        public void Dispose() { }

        public override void Draw()
        {
            // Window Settings
            var lockAllWindows = configuration.LockAllWindows;
            if (ImGui.Checkbox("Lock Game Windows in Place", ref lockAllWindows))
            {
                configuration.LockAllWindows = lockAllWindows;
                configuration.Save();
            }

            var showTitleBars = configuration.ShowDalamudTitleBars;
            if (ImGui.Checkbox("Show Dalamud Title Bars", ref showTitleBars))
            {
                configuration.ShowDalamudTitleBars = showTitleBars;
                configuration.Save();
            }

            ImGui.Separator();
            ImGui.Text("UI Scaling");

            // --- Corrected UI Scale Slider ---
            var tempScale = configuration.CustomUiScale; // 1. Use a temporary variable
            if (ImGui.SliderFloat("Overall Scale", ref tempScale, 0.5f, 3.0f))
            {
                configuration.CustomUiScale = tempScale; // 2. Update config from the temp variable
                configuration.Save();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Adjust the size of all Arena windows and elements.");
            }

            ImGui.Separator();
            ImGui.Text("Audio");

            // Mute Checkboxes
            var isBgmMuted = configuration.IsBgmMuted;
            if (ImGui.Checkbox("Mute Music", ref isBgmMuted))
            {
                configuration.IsBgmMuted = isBgmMuted;
                plugin.AudioManager.UpdateBgmState();
                configuration.Save();
            }

            ImGui.SameLine();

            var isSfxMuted = configuration.IsSfxMuted;
            if (ImGui.Checkbox("Mute SFX", ref isSfxMuted))
            {
                configuration.IsSfxMuted = isSfxMuted;
                configuration.Save();
            }

            // --- Corrected Volume Sliders ---
            var musicVolume = configuration.MusicVolume; // Use temp variable
            if (ImGui.SliderFloat("Music Volume", ref musicVolume, 0.0f, 1.0f))
            {
                configuration.MusicVolume = musicVolume;
                plugin.AudioManager.SetBgmVolume(musicVolume);
            }
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                configuration.Save();
            }

            var sfxVolume = configuration.SfxVolume; // Use temp variable
            if (ImGui.SliderFloat("SFX Volume", ref sfxVolume, 0.0f, 1.0f))
            {
                configuration.SfxVolume = sfxVolume;
            }
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                plugin.AudioManager.PlaySfx("menuselect.wav");
                configuration.Save();
            }
        }
    }
}

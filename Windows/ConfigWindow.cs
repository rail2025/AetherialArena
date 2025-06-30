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
            this.Size = new Vector2(300, 200);
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

            // Volume Sliders
            var musicVolume = configuration.MusicVolume;
            if (ImGui.SliderFloat("Music Volume", ref musicVolume, 0.0f, 1.0f))
            {
                configuration.MusicVolume = musicVolume;
                plugin.AudioManager.SetBgmVolume(musicVolume);
                // No need to save on drag, can save on release if preferred
            }
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                configuration.Save();
            }

            var sfxVolume = configuration.SfxVolume;
            if (ImGui.SliderFloat("SFX Volume", ref sfxVolume, 0.0f, 1.0f))
            {
                configuration.SfxVolume = sfxVolume;
                // SFX volume is applied on play, but you could play a test sound here
            }
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                // Play a test sound to confirm volume
                plugin.AudioManager.PlaySfx("menuselect.wav");
                configuration.Save();
            }
        }
    }
}

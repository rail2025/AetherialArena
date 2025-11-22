using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace AetherialArena.Windows
{
    public class ConfigWindow : Window, IDisposable
    {
        private readonly Plugin plugin;
        private readonly Configuration configuration;
        private bool isConfirmResetPopupOpen = true;

        public ConfigWindow(Plugin plugin) : base("Aetherial Arena - Settings")
        {
            this.Size = new Vector2(300, 350);
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

            if (ImGui.Button("Reset##Scale"))
            {
                configuration.CustomUiScale = 1.0f;
                configuration.Save();
            }
            ImGui.SameLine();
            var tempScale = configuration.CustomUiScale;
            if (ImGui.InputFloat("##CustomScaleInput", ref tempScale, 0.1f, 0.2f, "%.2f"))
            {
                configuration.CustomUiScale = Math.Clamp(tempScale, 0.5f, 3.0f);
                configuration.Save();
            }
                        
            if (ImGui.SliderFloat("Overall Scale", ref tempScale, 0.5f, 3.0f))
            {
                configuration.CustomUiScale = tempScale;
                configuration.Save();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Adjust the size of all Arena windows and elements.");
            }

            

            ImGui.Separator();
            ImGui.Text("Audio");

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

            var musicVolume = configuration.MusicVolume;
            if (ImGui.SliderFloat("Music Volume", ref musicVolume, 0.0f, 1.0f))
            {
                configuration.MusicVolume = musicVolume;
                plugin.AudioManager.SetBgmVolume(musicVolume);
            }
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                configuration.Save();
            }

            var sfxVolume = configuration.SfxVolume;
            if (ImGui.SliderFloat("SFX Volume", ref sfxVolume, 0.0f, 1.0f))
            {
                configuration.SfxVolume = sfxVolume;
            }
            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                plugin.AudioManager.PlaySfx("menuselect.wav");
                configuration.Save();
            }

            ImGui.Separator();
            ImGui.Text("Data Management");

            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.2f, 0.2f, 1.0f));
            ImGui.TextWrapped("WARNING: This will delete all of your captured sprites, levels, and progress.");
            ImGui.PopStyleColor();

            if (ImGui.Button("Reset All Player Data"))
            {
                ImGui.OpenPopup("Confirm Reset");
            }

            var center = ImGui.GetMainViewport().GetCenter();
            ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

            if (ImGui.BeginPopupModal("Confirm Reset", ref isConfirmResetPopupOpen, ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.Text("Are you absolutely sure?\nThis cannot be undone.");
                ImGui.Separator();

                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.1f, 0.1f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.2f, 0.2f, 1.0f));
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.5f, 0.05f, 0.05f, 1.0f));
                if (ImGui.Button("Yes, Reset Everything", new Vector2(180, 0)))
                {
                    plugin.ResetPlayerProfile();
                    ImGui.CloseCurrentPopup();
                }
                ImGui.PopStyleColor(3);

                ImGui.SameLine();

                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                {
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }
    }
}

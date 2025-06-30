using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Text.Json.Serialization;

namespace AetherialArena
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public bool LockAllWindows { get; set; } = true;
        public bool ShowDalamudTitleBars { get; set; } = false;

        // Audio Settings
        public bool IsBgmMuted { get; set; } = false;
        public bool IsSfxMuted { get; set; } = false;
        public float MusicVolume { get; set; } = 0.5f;
        public float SfxVolume { get; set; } = 0.8f;

        [JsonIgnore]
        private IDalamudPluginInterface? pluginInterface;

        public void Initialize(IDalamudPluginInterface pInterface)
        {
            this.pluginInterface = pInterface;
        }

        public void Save()
        {
            this.pluginInterface!.SavePluginConfig(this);
        }
    }
}

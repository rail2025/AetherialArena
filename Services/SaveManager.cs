using System.IO;
using System.Text.Json;
using AetherialArena.Models;

namespace AetherialArena.Services
{
    /// <summary>
    /// Handles saving and loading the player's profile data.
    /// </summary>
    public class SaveManager
    {
        private readonly string profilePath;

        public SaveManager()
        {
            // Get the path to the plugin's configuration directory
            var configDir = Plugin.PluginInterface.GetPluginConfigDirectory();
            profilePath = Path.Combine(configDir, "PlayerProfile.json");
        }

        public PlayerProfile LoadProfile()
        {
            if (!File.Exists(profilePath))
            {
                Plugin.Log.Info("No player profile found, creating a new one.");
                return new PlayerProfile();
            }

            try
            {
                var json = File.ReadAllText(profilePath);
                var profile = JsonSerializer.Deserialize<PlayerProfile>(json);
                Plugin.Log.Info("Player profile loaded successfully.");
                return profile ?? new PlayerProfile();
            }
            catch (System.Exception ex)
            {
                Plugin.Log.Error(ex, "Failed to load player profile, creating a new one.");
                return new PlayerProfile(); // Return a fresh profile on error
            }
        }

        public void SaveProfile(PlayerProfile profile)
        {
            try
            {
                var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(profilePath, json);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.Error(ex, "Failed to save player profile.");
            }
        }
    }
}

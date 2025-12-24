using System;
using System.IO;
using System.Text.Json;
using AetherialArena.Models;

namespace AetherialArena.Services
{
    public class SaveManager
    {
        private readonly string profilePath;

        public SaveManager()
        {
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

            PlayerProfile? profile = null;

            try
            {
                var json = File.ReadAllText(profilePath);
                profile = JsonSerializer.Deserialize<PlayerProfile>(json);

                if (profile != null)
                {
                    var timePassed = DateTime.UtcNow - profile.LastAetherRegenTimestamp;
                    var regenIntervalMinutes = 10; // The time it takes to regen 1 Aether

                    if (timePassed.TotalMinutes > 0)
                    {
                        // Calculate how many regen intervals have occurred
                        int intervalsPassed = (int)(timePassed.TotalMinutes / regenIntervalMinutes);

                        if (intervalsPassed > 0)
                        {
                            //Plugin.Log.Info($"Player was offline for {timePassed.TotalMinutes:F0} minutes. Granting {intervalsPassed} Aether.");
                            profile.CurrentAether = Math.Min(profile.MaxAether, profile.CurrentAether + intervalsPassed);
                            profile.LastAetherRegenTimestamp = profile.LastAetherRegenTimestamp.AddMinutes(intervalsPassed * regenIntervalMinutes);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.Error(ex, "Failed to load player profile, creating a new one.");
                return new PlayerProfile(); 
            }

            Plugin.Log.Info("Player profile loaded successfully.");
            return profile ?? new PlayerProfile();
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

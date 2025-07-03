using System;
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

            // Declare the profile variable here to widen its scope.
            PlayerProfile? profile = null;

            try
            {
                var json = File.ReadAllText(profilePath);
                // Assign the deserialized object to the variable.
                profile = JsonSerializer.Deserialize<PlayerProfile>(json);

                if (profile != null)
                {
                    // Calculate time passed since the profile was last saved
                    var timePassed = DateTime.UtcNow - profile.LastAetherRegenTimestamp;
                    var regenIntervalMinutes = 10; // The time it takes to regen 1 Aether

                    if (timePassed.TotalMinutes > 0)
                    {
                        // Calculate how many regen intervals have occurred
                        int intervalsPassed = (int)(timePassed.TotalMinutes / regenIntervalMinutes);

                        if (intervalsPassed > 0)
                        {
                            Plugin.Log.Info($"Player was offline for {timePassed.TotalMinutes:F0} minutes. Granting {intervalsPassed} Aether.");
                            // Add the regenerated Aether, capped at the max
                            profile.CurrentAether = Math.Min(profile.MaxAether, profile.CurrentAether + intervalsPassed);

                            // Update the timestamp to prevent granting the same time again.
                            profile.LastAetherRegenTimestamp = profile.LastAetherRegenTimestamp.AddMinutes(intervalsPassed * regenIntervalMinutes);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.Error(ex, "Failed to load player profile, creating a new one.");
                return new PlayerProfile(); // Return a fresh profile on error
            }

            Plugin.Log.Info("Player profile loaded successfully.");
            // Because 'profile' was declared outside the try block, it is now accessible here.
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AetherialArena.Models;

namespace AetherialArena.Services
{
    public class DataManager
    {
        public List<Sprite> Sprites { get; } = new();
        public List<EncounterData> Encounters { get; } = new();

        public DataManager()
        {
            LoadData();
        }

        private void LoadData()
        {
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() },
                PropertyNameCaseInsensitive = true
            };

            Sprites.AddRange(LoadJson<List<Sprite>>("sprites.json", options));
            Encounters.AddRange(LoadJson<List<EncounterData>>("encountertables.json", options));
        }

        private T LoadJson<T>(string fileName, JsonSerializerOptions options) where T : new()
        {
            var assembly = Assembly.GetExecutingAssembly();

            // This is the new, robust method. It finds the correct path dynamically.
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(str => str.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(resourceName))
            {
                Plugin.Log.Error($"Could not find the embedded resource '{fileName}'. Please ensure it is in the project and its Build Action is 'Embedded Resource'.");
                return new T();
            }

            Plugin.Log.Info($"Found and attempting to load resource: {resourceName}");

            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                using var reader = new StreamReader(stream!);
                var json = reader.ReadToEnd();
                return JsonSerializer.Deserialize<T>(json, options) ?? new T();
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, $"An exception occurred while loading {fileName}.");
            }
            return new T();
        }
    }
}

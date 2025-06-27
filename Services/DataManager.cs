using AetherialArena.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace AetherialArena.Services
{
    public class DataManager
    {
        public List<Sprite> AllSprites { get; private set; } = new();
        public Dictionary<string, Sprite> AllSpriteData { get; private set; } = new();

        public DataManager()
        {
            this.AllSprites = LoadDataFromEmbed<List<Sprite>>("sprites.json") ?? new List<Sprite>();
            this.AllSpriteData = LoadDataFromEmbed<Dictionary<string, Sprite>>("spritedatanolocation.json") ?? new Dictionary<string, Sprite>();
        }

        public Sprite? GetSpriteData(int id)
        {
            return AllSpriteData.GetValueOrDefault(id.ToString());
        }

        private T? LoadDataFromEmbed<T>(string fileName) where T : class
        {
            // Corrected path to include the 'assets' folder
            var resourcePath = $"AetherialArena.assets.data.{fileName}";
            var assembly = Assembly.GetExecutingAssembly();

            using var stream = assembly.GetManifestResourceStream(resourcePath);
            if (stream == null)
            {
                Plugin.Log.Error($"Failed to load embedded resource: {resourcePath}. Make sure the file's 'Build Action' is set to 'Embedded Resource'.");
                return null;
            }

            using var reader = new StreamReader(stream);
            var jsonContent = reader.ReadToEnd();
            return JsonConvert.DeserializeObject<T>(jsonContent);
        }
    }
}

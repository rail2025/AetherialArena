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
        public List<Ability> Abilities { get; } = new();
        private readonly Dictionary<int, string> locationHints = new();

        public DataManager()
        {
            LoadData();
            LoadHints();
        }

        public Sprite? GetSpriteData(int id) => Sprites.FirstOrDefault(s => s.ID == id);
        public string GetHint(int spriteId) => locationHints.GetValueOrDefault(spriteId, string.Empty);
        public Ability? GetAbility(int id) => Abilities.FirstOrDefault(a => a.ID == id);

        private void LoadData()
        {
            var options = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() },
                PropertyNameCaseInsensitive = true
            };

            var loadedSprites = LoadJson<List<Sprite>>("sprites.json", options);
            var detailedSpriteList = LoadJson<List<Sprite>>("spritedatanolocation.json", options);
            var loadedAbilities = LoadJson<List<Ability>>("ability.json", options);
            var loadedEncounters = LoadJson<List<EncounterData>>("encountertables.json", options);

            if (loadedSprites == null || loadedAbilities == null) return;

            Abilities.AddRange(loadedAbilities);
            if (loadedEncounters != null) Encounters.AddRange(loadedEncounters);

            var detailedDataDict = detailedSpriteList?.ToDictionary(s => s.ID);
            var abilitiesDict = Abilities.ToDictionary(a => a.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var sprite in loadedSprites)
            {
                if (detailedDataDict != null && detailedDataDict.TryGetValue(sprite.ID, out var details))
                {
                    sprite.SubType = details.SubType;
                    sprite.AttackType = details.AttackType;
                    sprite.Weaknesses = details.Weaknesses;
                    sprite.Resistances = details.Resistances;
                }

                var abilityName = sprite.SpecialAbility switch
                {
                    "Damage Up 15%" => "Empower",
                    "Shield 25%" => "Barrier",
                    _ => sprite.SpecialAbility
                };

                if (abilitiesDict.TryGetValue(abilityName, out var mappedAbility))
                {
                    sprite.SpecialAbilityID = mappedAbility.ID;
                }

                sprite.RecolorKey = sprite.Rarity switch
                {
                    RarityTier.Uncommon => "green",
                    RarityTier.Rare => "purple",
                    _ => sprite.Type switch
                    {
                        SpriteType.Beast => "orange",
                        SpriteType.Mechanical => "darkred",
                        _ => "default"
                    }
                };
            }

            Sprites.AddRange(loadedSprites);
        }

        private void LoadHints()
        {
            locationHints[4] = "a bunch of lunatics!";
            locationHints[5] = "Lookout!";
            locationHints[9] = "ant ant ant";
            locationHints[10] = "gem exchange";
            locationHints[14] = "light on a hill";
            locationHints[15] = "long way down!";
            locationHints[19] = "Infernal troupe";
            locationHints[20] = "drill baby drill";
            locationHints[24] = "yellow formation";
            locationHints[25] = "thats a lotta bull";
            locationHints[29] = "if you want some eggs...";
            locationHints[30] = "that's not a big scary hole...";
            locationHints[34] = "guiding light";
            locationHints[35] = "of course theres one all the way out there";
            locationHints[39] = "Cliffside skull";
            locationHints[40] = "hovering colors";
            locationHints[44] = "halitosis";
            locationHints[45] = "saddest wings on a lizard ever";
            locationHints[49] = "2 become 1";
            locationHints[50] = "purple, no lavender, no lilac! is my color";
            locationHints[54] = "Iron cutting sword";
            locationHints[55] = "hot air balloon";
            locationHints[59] = "mead lover";
            locationHints[60] = "judge not lest ye be";
            locationHints[64] = "is that a carnival ride or a telescope";
            locationHints[65] = "mountain tunnel";
            locationHints[69] = "Shard gate";
            locationHints[70] = "moon bride memorial";
        }

        private T? LoadJson<T>(string fileName, JsonSerializerOptions options) where T : class, new()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(str => str.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(resourceName))
            {
                Plugin.Log.Error($"Could not find the embedded resource '{fileName}'.");
                return new T();
            }
            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null) return null;
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                return JsonSerializer.Deserialize<T>(json, options);
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, $"An exception occurred while loading {fileName}.");
                return null;
            }
        }
    }
}

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
        public readonly Dictionary<int, (string Name, uint Id)> MinionUnlockMap;
        public Dictionary<ushort, List<string>> TerritoryBackgrounds { get; } = new();
        private readonly Random random = new();
        public Dictionary<int, string> SpriteLocations { get; } = new();

        public DataManager()
        {
            MinionUnlockMap = new()
            {
                { 1, ("Wind-up Airship", 52) }, { 2, ("Goobbue Sproutling", 41) }, { 3, ("Buffalo Calf", 32) },
                { 4, ("Midgardsormr", 119) }, { 5, ("Morbol Seedling", 12) }, { 6, ("Wind-up Onion Knight", 92) },
                { 7, ("Puff of Darkness", 101) }, { 8, ("Wind-up Cursor", 51) }, { 9, ("Black Chocobo Chick", 54) },
                { 10, ("Minion of Light", 67) }, { 11, ("Wide-eyed Fawn", 17) }, { 12, ("Dust Bunny", 28) },
                { 13, ("Beady Eye", 36) }, { 14, ("Princely Hatchling", 75) }, { 15, ("Fledgling Dodo", 37) },
                { 16, ("Coeurl Kitten", 19) }, { 17, ("Wind-up Leader", 71) }, { 18, ("Wind-up Odin", 76) },
                { 19, ("Wolf Pup", 35) }, { 20, ("Wind-up Warrior of Light", 77) }, { 21, ("Mammet #001", 2) },
                { 22, ("Cherry Bomb", 1) }, { 23, ("Wind-up Gentleman", 21) }, { 24, ("Wind-up Nanamo", 84) },
                { 25, ("Wayward Hatchling", 3) }, { 26, ("Wind-up Goblin", 49) }, { 27, ("Wind-up Gilgamesh", 85) },
                { 28, ("Slime Puddle", 47) }, { 29, ("Wind-up Ultros", 104) }, { 30, ("Bite-sized Pudding", 42) },
                { 31, ("Enkidu", 122) }, { 32, ("Pudgy Puk", 31) }, { 33, ("Baby Bun", 14) },
                { 34, ("Kidragora", 48) }, { 35, ("Coblyn Larva", 38) }, { 36, ("Chigoe Larva", 15) },
                { 37, ("Smallshell", 34) }, { 38, ("Demon Brick", 44) }, { 39, ("Infant Imp", 18) },
                { 40, ("Tight-beaked Parrot", 57) }, { 41, ("Mummy's Little Mummy", 112) }, { 42, ("Fat Cat", 110) },
                { 43, ("Baby Opo-opo", 80) }, { 44, ("Naughty Nanka", 102) }, { 45, ("Wind-up Louisoix", 118) },
                { 46, ("Gravel Golem", 22) }, { 47, ("Plush Cushion", 66) }, { 48, ("Tiny Rat", 13) },
                { 49, ("Bluebird", 16) }, { 50, ("Minute Mindflayer", 56) }, { 51, ("Cactuar Cutting", 33) },
                { 52, ("Baby Raptor", 25) }, { 53, ("Baby Bat", 26) }, { 54, ("Nutkin", 97) },
                { 55, ("Tiny Bulb", 27) }, { 56, ("Magic Broom", 81) }, { 57, ("Nana Bear", 95) },
                { 58, ("Model Vanguard", 43) }, { 59, ("Tiny Tortoise", 24) }, { 60, ("Wind-up Dullahan", 29) },
                { 61, ("Wind-up Tonberry", 23) }, { 62, ("Miniature Minecart", 96) }, { 63, ("Black Coeurl", 20) },
                { 64, ("Wind-up Aldgoat", 39) }, { 65, ("Wind-up Sun", 65) }, { 66, ("Tiny Tapir", 94) },
                { 67, ("Wind-up Qiqirn", 54) }, { 68, ("Onion Prince", 86) }, { 69, ("Treasure Box", 93) },
                { 70, ("Heavy Hatchling", 106) }
            };

            LoadData();
            LoadHints();
            LoadBackgroundMappings();
            LoadLocations();
        }

        public Sprite? GetSpriteData(int id) => Sprites.FirstOrDefault(s => s.ID == id);
        public Ability? GetAbility(int id) => Abilities.FirstOrDefault(a => a.ID == id);
        public string GetHint(int spriteId) => locationHints.GetValueOrDefault(spriteId, string.Empty);
        public string GetLocation(int spriteId) => SpriteLocations.GetValueOrDefault(spriteId, string.Empty);


        public string GetBackgroundForTerritory(ushort territoryId)
        {
            if (TerritoryBackgrounds.TryGetValue(territoryId, out var backgroundList) && backgroundList.Any())
            {
                return backgroundList[random.Next(backgroundList.Count)];
            }

            return $"background{random.Next(1, 8)}.png";
        }

        private void LoadBackgroundMappings()
        {
            var laNosceaBgs = new List<string> { "background5.png", "background2.png", "background1.png" };
            var thanalanBgs = new List<string> { "background3.png", "background2.png", "background1.png" };
            var shroudBgs = new List<string> { "background4.png", "background2.png", "background1.png" };
            var coerthasBgs = new List<string> { "background7.png", "background2.png", "background1.png" };
            var morDhonaBgs = new List<string> { "background6.png", "background2.png", "background1.png" };

            // --- Assign lists to territory IDs ---
            new ushort[] { 128, 134, 135, 137, 138, 139, 180 }.ToList().ForEach(id => TerritoryBackgrounds[id] = laNosceaBgs);
            new ushort[] { 140, 141, 145, 146, 147 }.ToList().ForEach(id => TerritoryBackgrounds[id] = thanalanBgs);
            new ushort[] { 148, 152, 153, 154 }.ToList().ForEach(id => TerritoryBackgrounds[id] = shroudBgs);
            new ushort[] { 155 }.ToList().ForEach(id => TerritoryBackgrounds[id] = coerthasBgs);
            new ushort[] { 156 }.ToList().ForEach(id => TerritoryBackgrounds[id] = morDhonaBgs);
        }

        private void LoadLocations()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(str => str.EndsWith("PlaceName-arrzonescombined.csv", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(resourceName)) return;

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return;
            using var reader = new StreamReader(stream);

            string? line;
            reader.ReadLine();
            while ((line = reader.ReadLine()) != null)
            {
                var parts = line.Split(',');

                // sprite ID is at index 1. Location name is the 4th from the end.
                if (parts.Length >= 5 && int.TryParse(parts[1], out int spriteId))
                {
                    // The location name is the 4th element from the end of the row
                    string locationName = parts[parts.Length - 4].Trim().Replace("\"", "");
                    if (!string.IsNullOrEmpty(locationName))
                    {
                        SpriteLocations[spriteId] = locationName;
                    }
                }
            }
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
            if (loadedEncounters != null)
            {
                foreach (var encounter in loadedEncounters)
                {
                    if (encounter.EncountersBySubLocation == null)
                    {
                        encounter.EncountersBySubLocation = new Dictionary<string, List<int>>();
                    }
                    if (encounter.Default != null && encounter.Default.Any())
                    {
                        encounter.EncountersBySubLocation["Default"] = encounter.Default;
                    }
                }
                Encounters.AddRange(loadedEncounters);
            }
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

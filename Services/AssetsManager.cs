using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace AetherialArena.Services
{
    public class AssetManager : IDisposable
    {
        private readonly Dictionary<string, IDalamudTextureWrap?> loadedTextures = new();

        public IDalamudTextureWrap? GetIcon(string iconName)
        {
            if (string.IsNullOrEmpty(iconName)) return null;

            if (loadedTextures.TryGetValue(iconName, out var texture))
            {
                return texture;
            }

            var stream = GetStreamFromEmbeddedResource(iconName);

            if (stream == null)
            {
                Plugin.Log.Error($"Failed to load icon resource: {iconName}. This icon will not be checked again this session.");
                loadedTextures.Add(iconName, null);
                return null;
            }

            try
            {
                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                var newTexture = Plugin.TextureProvider.CreateFromImageAsync(memoryStream.ToArray()).Result;
                loadedTextures.Add(iconName, newTexture);
                return newTexture;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, $"Exception loading icon: {iconName}");
                loadedTextures.Add(iconName, null);
                return null;
            }
            finally
            {
                stream.Dispose();
            }
        }

        private Stream? GetStreamFromEmbeddedResource(string iconName)
        {
            var assembly = GetType().Assembly;

            // Define a list of possible paths to check
            var possiblePaths = new List<string>
            {
                $"AetherialArena.Assets.Icons.{iconName}", // Original path
                $"AetherialArena.Assets.{iconName}",      // Check in root of Assets
                $"AetherialArena.{iconName}"              // Check in root of project
            };

            foreach (var path in possiblePaths)
            {
                var stream = assembly.GetManifestResourceStream(path);
                if (stream != null)
                {
                    Plugin.Log.Info($"Found and attempting to load resource: {path}");
                    return stream;
                }
            }

            // Also try lowercase versions as a fallback
            foreach (var path in possiblePaths)
            {
                var stream = assembly.GetManifestResourceStream(path.ToLower());
                if (stream != null)
                {
                    Plugin.Log.Info($"Found and attempting to load resource: {path.ToLower()}");
                    return stream;
                }
            }

            return null;
        }

        public void Dispose()
        {
            foreach (var texture in loadedTextures.Values)
            {
                texture?.Dispose();
            }
            loadedTextures.Clear();
        }
    }
}

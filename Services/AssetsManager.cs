using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using System;
using System.Collections.Generic;
using System.IO;

namespace AetherialArena.Services
{
    public class AssetManager : IDisposable
    {
        private readonly Dictionary<string, IDalamudTextureWrap> loadedTextures = new();

        public IDalamudTextureWrap? GetIcon(string iconName)
        {
            if (string.IsNullOrEmpty(iconName)) return null;

            if (loadedTextures.TryGetValue(iconName, out var texture))
            {
                return texture;
            }

            var assembly = GetType().Assembly;

            // Define both possible paths to handle the csproj inconsistency.
            var pathUpperCase = $"AetherialArena.Assets.Icons.{iconName}";
            var pathLowerCase = $"AetherialArena.assets.icons.{iconName}";

            // Try the uppercase path first, as seen for icons in the .csproj
            var stream = assembly.GetManifestResourceStream(pathUpperCase);

            // If the uppercase path fails, try the lowercase path as a fallback.
            if (stream == null)
            {
                stream = assembly.GetManifestResourceStream(pathLowerCase);
            }

            // If both paths fail, then the file is truly not embedded correctly.
            if (stream == null)
            {
                Plugin.Log.Error($"Failed to load icon resource. Tried both paths: '{pathUpperCase}' and '{pathLowerCase}'");
                return null;
            }

            try
            {
                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                var newTexture = Plugin.TextureProvider.CreateFromImageAsync(memoryStream.ToArray()).Result;
                loadedTextures.Add(iconName, newTexture);
                stream.Dispose(); // Dispose the stream after use
                return newTexture;
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, $"Exception loading icon: {iconName}");
                stream.Dispose();
                return null;
            }
        }

        public void Dispose()
        {
            foreach (var texture in loadedTextures.Values)
            {
                texture.Dispose();
            }
            loadedTextures.Clear();
        }
    }
}

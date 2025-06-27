using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;

namespace AetherialArena.Services
{
    public class AssetManager : IDisposable
    {
        private readonly ITextureProvider textureProvider;
        private readonly IDalamudPluginInterface pluginInterface;
        private readonly Dictionary<string, IDalamudTextureWrap> textureCache = new();

        public AssetManager(ITextureProvider textureProvider, IDalamudPluginInterface pluginInterface)
        {
            this.textureProvider = textureProvider;
            this.pluginInterface = pluginInterface;
        }

        public IDalamudTextureWrap? GetTexture(string path)
        {
            if (textureCache.TryGetValue(path, out var texture))
            {
                return texture;
            }

            try
            {
                var newTexture = this.textureProvider.GetFromGame(path) ?? this.textureProvider.GetFromDalamud(path);

                if (newTexture != null)
                {
                    textureCache[path] = newTexture;
                    return newTexture;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, $"Failed to get texture from path: {path}");
                return null;
            }

            return null;
        }

        public IDalamudTextureWrap? GetEmbed(string resourcePath)
        {
            if (textureCache.TryGetValue(resourcePath, out var texture))
            {
                return texture;
            }

            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourcePath);

            if (stream != null)
            {
                var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                var rawImage = memoryStream.ToArray();

                var newTexture = this.textureProvider.CreateFromImageAsync(rawImage).Result;
                if (newTexture != null)
                {
                    textureCache[resourcePath] = newTexture;
                    return newTexture;
                }
            }
            else
            {
                Plugin.Log.Error($"Failed to load embedded resource: {resourcePath}.");
            }

            return null;
        }

        public void Dispose()
        {
            foreach (var texture in textureCache.Values)
            {
                texture?.Dispose();
            }
            textureCache.Clear();
        }
    }
}

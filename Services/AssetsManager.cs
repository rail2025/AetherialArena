using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;

namespace AetherialArena.Services
{
    public class AssetManager : IDisposable
    {
        private readonly ITextureProvider textureProvider;
        private readonly ConcurrentDictionary<string, IDalamudTextureWrap?> textureCache = new();
        private readonly ConcurrentDictionary<string, IDalamudTextureWrap?> chromaKeyedTextureCache = new();
        private readonly ConcurrentDictionary<(string, string, bool), IDalamudTextureWrap?> recoloredTextureCache = new();
        private readonly HashSet<string> requestedAssets = new();

        private static readonly Dictionary<string, (float hueShift, float satMult, float valMult)> RecolorProfiles = new()
        {
            { "purple", (0.75f, 1.2f, 0.9f) },
            { "orange", (0.1f, 1.3f, 1.0f) },
            { "green", (0.35f, 1.1f, 0.95f) },
            { "darkred", (0.0f, 1.5f, 0.7f) }
        };

        public AssetManager(IDalamudPluginInterface pluginInterface, ITextureProvider textureProvider)
        {
            this.textureProvider = textureProvider;
        }

        private Image<Rgba32> RemoveBlackBackground(Image<Rgba32> image)
        {
            /*image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var pixelRow = accessor.GetRowSpan(y);
                    foreach (ref var pixel in pixelRow)
                    {
                        if (pixel.R < 7 && pixel.G < 7 && pixel.B < 7)
                        {
                            pixel.A = 0;
                        }
                    }
                }
            });*/
            return image;
        }

        private void ApplyHsvShift(Image<Rgba32> image, string recolorKey)
        {
            if (!RecolorProfiles.TryGetValue(recolorKey, out var profile)) return;

            image.Mutate(ctx =>
            {
                ctx.ProcessPixelRowsAsVector4(row =>
                {
                    for (int x = 0; x < row.Length; x++)
                    {
                        Vector4 pixel = row[x];
                        if (pixel.W == 0) continue;
                        RgbToHsv(pixel, out float h, out float s, out float v);
                        h = (h + profile.hueShift) % 1.0f;
                        s = Math.Clamp(s * profile.satMult, 0, 1);
                        v = Math.Clamp(v * profile.valMult, 0, 1);
                        row[x] = HsvToRgb(h, s, v, pixel.W);
                    }
                });
            });
        }

        private Stream? GetStreamFromEmbeddedResource(string iconName)
        {
            Plugin.Log.Info($"AssetManager: Requesting resource '{iconName}'.");
            var assembly = GetType().Assembly;
            string assemblyName = assembly.GetName().Name ?? "AetherialArena";

            var possiblePaths = new[]
            {
                $"{assemblyName}.Assets.Icons.{iconName}",
                $"{assemblyName}.Assets.Backgrounds.{iconName}",
                $"{assemblyName}.{iconName}"
            };

            Plugin.Log.Info($"AssetManager: Searching explicit paths: {string.Join(", ", possiblePaths)}");

            var resourceNames = new HashSet<string>(assembly.GetManifestResourceNames());
            var resourcePath = possiblePaths.FirstOrDefault(p => resourceNames.Contains(p));

            if (resourcePath != null)
            {
                Plugin.Log.Info($"AssetManager: Found resource at explicit path: '{resourcePath}'.");
                return assembly.GetManifestResourceStream(resourcePath);
            }

            Plugin.Log.Warning($"AssetManager: Could not find '{iconName}' at explicit paths. Falling back to ambiguous search.");
            resourcePath = resourceNames.FirstOrDefault(str => str.EndsWith(iconName, StringComparison.OrdinalIgnoreCase));

            if (resourcePath != null)
            {
                Plugin.Log.Info($"AssetManager: Found resource via fallback search: '{resourcePath}'.");
                return assembly.GetManifestResourceStream(resourcePath);
            }

            Plugin.Log.Error($"AssetManager: FAILED to find any embedded resource for '{iconName}'. Please ensure the file exists at the correct path and its 'Build Action' is set to 'Embedded Resource'.");
            return null;
        }

        public IDalamudTextureWrap? GetIcon(string iconName, bool removeBlackBg = false)
        {
            if (string.IsNullOrEmpty(iconName)) return null;
            var cache = removeBlackBg ? chromaKeyedTextureCache : textureCache;
            var requestKey = iconName + removeBlackBg;

            if (cache.TryGetValue(iconName, out var texture)) return texture;

            if (requestedAssets.Add(requestKey))
            {
                Task.Run(async () =>
                {
                    try
                    {
                        using var stream = GetStreamFromEmbeddedResource(iconName);
                        if (stream == null) { cache[iconName] = null; return; }

                        var image = Image.Load<Rgba32>(stream!);
                        if (removeBlackBg) image = RemoveBlackBackground(image);

                        using var ms = new MemoryStream();
                        await image.SaveAsPngAsync(ms);
                        ms.Position = 0;
                        cache[iconName] = await textureProvider.CreateFromImageAsync(ms);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Error(ex, $"Failed to load icon: {iconName}");
                        cache[iconName] = null;
                    }
                });
            }
            return null;
        }

        public IDalamudTextureWrap? GetRecoloredIcon(string iconName, string recolorKey, bool removeBlackBg = false)
        {
            if (string.IsNullOrEmpty(iconName) || recolorKey == "default")
                return GetIcon(iconName, removeBlackBg);

            var cacheKey = (iconName, recolorKey, removeBlackBg);
            var requestKey = iconName + recolorKey + removeBlackBg;

            if (recoloredTextureCache.TryGetValue(cacheKey, out var texture)) return texture;

            if (requestedAssets.Add(requestKey))
            {
                Task.Run(async () =>
                {
                    try
                    {
                        using var stream = GetStreamFromEmbeddedResource(iconName);
                        if (stream == null) { recoloredTextureCache[cacheKey] = null; return; }

                        var image = Image.Load<Rgba32>(stream!);
                        if (removeBlackBg) image = RemoveBlackBackground(image);
                        ApplyHsvShift(image, recolorKey);

                        using var ms = new MemoryStream();
                        await image.SaveAsPngAsync(ms);
                        ms.Position = 0;
                        recoloredTextureCache[cacheKey] = await textureProvider.CreateFromImageAsync(ms);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Error(ex, $"Failed to recolor icon: {iconName}");
                        recoloredTextureCache[cacheKey] = null;
                    }
                });
            }
            return null;
        }

        private static void RgbToHsv(Vector4 rgb, out float h, out float s, out float v)
        {
            float max = Math.Max(rgb.X, Math.Max(rgb.Y, rgb.Z));
            float min = Math.Min(rgb.X, Math.Min(rgb.Y, rgb.Z));
            float delta = max - min;

            h = 0f;
            if (delta > 0)
            {
                if (Math.Abs(max - rgb.X) < 1e-3f)
                    h = (rgb.Y - rgb.Z) / delta;
                else if (Math.Abs(max - rgb.Y) < 1e-3f)
                    h = 2f + (rgb.Z - rgb.X) / delta;
                else
                    h = 4f + (rgb.X - rgb.Y) / delta;

                h *= 60f;
                if (h < 0) h += 360f;
            }

            h /= 360f;
            s = max == 0 ? 0 : delta / max;
            v = max;
        }

        private static Vector4 HsvToRgb(float h, float s, float v, float a)
        {
            h *= 360f;
            float c = v * s;
            float x = c * (1 - Math.Abs((h / 60f) % 2 - 1));
            float m = v - c;

            float r, g, b;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return new Vector4(r + m, g + m, b + m, a);
        }

        public void Dispose()
        {
            foreach (var tex in textureCache.Values) tex?.Dispose();
            foreach (var tex in chromaKeyedTextureCache.Values) tex?.Dispose();
            foreach (var tex in recoloredTextureCache.Values) tex?.Dispose();
            textureCache.Clear();
            chromaKeyedTextureCache.Clear();
            recoloredTextureCache.Clear();
            requestedAssets.Clear();
        }
    }
}

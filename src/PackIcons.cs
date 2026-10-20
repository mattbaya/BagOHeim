using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BagOHeim
{
    /// <summary>
    /// Builds each pack's inventory icon: the bundled backpack picture with one of the game's own
    /// icons applied to the patch on its front. The game's artwork is read at runtime and never
    /// stored in the mod.
    /// </summary>
    internal static class PackIcons
    {
        private const string BagResource = "BagOHeim.bag.png";
        private const int Size = 128;
        // The patch on bag.png: centre (64, 88) from the top-left, so (64, 40) in texture rows,
        // with room for a square icon about 38 px across.
        private const int PatchCenterX = 64;
        private const int PatchCenterY = Size - 88;
        private const int PatchBox = 38;

        private static Color[] _bag;
        private static readonly Dictionary<Texture, Texture2D> Readable = new Dictionary<Texture, Texture2D>();

        /// <summary>The backpack with <paramref name="patch"/> on it, or null if it can't be built (caller uses the plain icon).</summary>
        public static Sprite Compose(Sprite patch)
        {
            if (patch == null || patch.texture == null) return null;
            // Dedicated servers have no graphics device and never show icons.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return null;

            try
            {
                var bag = BagPixels();
                if (bag == null) return null;

                var source = ReadableCopy(patch.texture);
                var rect = patch.textureRect;
                int sx = Mathf.RoundToInt(rect.x), sy = Mathf.RoundToInt(rect.y);
                int sw = Mathf.RoundToInt(rect.width), sh = Mathf.RoundToInt(rect.height);
                var src = source.GetPixels(sx, sy, sw, sh);

                var pixels = (Color[])bag.Clone();
                float scale = Mathf.Min((float)PatchBox / sw, (float)PatchBox / sh);
                int dw = Mathf.Max(1, Mathf.RoundToInt(sw * scale));
                int dh = Mathf.Max(1, Mathf.RoundToInt(sh * scale));
                int ox = PatchCenterX - dw / 2;
                int oy = PatchCenterY - dh / 2;

                for (int y = 0; y < dh; y++)
                {
                    for (int x = 0; x < dw; x++)
                    {
                        int px = ox + x, py = oy + y;
                        if (px < 0 || px >= Size || py < 0 || py >= Size) continue;

                        var s = Sample(src, sw, sh, (x + 0.5f) / dw * sw - 0.5f, (y + 0.5f) / dh * sh - 0.5f);
                        pixels[py * Size + px] = Over(s, pixels[py * Size + px]);
                    }
                }

                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                texture.SetPixels(pixels);
                texture.filterMode = FilterMode.Bilinear;
                texture.Apply();
                return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
            }
            catch (Exception e)
            {
                BagOHeimPlugin.Log.LogWarning($"Couldn't put an icon on the backpack; using the plain game icon. {e.Message}");
                return null;
            }
        }

        private static Color[] BagPixels()
        {
            if (_bag != null) return _bag;

            using (var stream = typeof(PackIcons).Assembly.GetManifestResourceStream(BagResource))
            {
                if (stream == null)
                {
                    BagOHeimPlugin.Log.LogWarning($"Missing embedded {BagResource}");
                    return null;
                }
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.LoadImage(bytes);
                if (texture.width != Size || texture.height != Size) return null;
                return _bag = texture.GetPixels();
            }
        }

        /// <summary>
        /// A CPU-readable copy of a texture. The game's icon textures aren't readable directly, so
        /// they're drawn to a render texture and read back (the standard way to do this in Unity).
        /// </summary>
        private static Texture2D ReadableCopy(Texture texture)
        {
            if (Readable.TryGetValue(texture, out var cached)) return cached;

            var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(texture, rt);
                RenderTexture.active = rt;
                var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                copy.Apply();
                Readable[texture] = copy;
                return copy;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>Frees the readable copies once all icons have been built.</summary>
        public static void ReleaseCopies()
        {
            foreach (var copy in Readable.Values)
                if (copy != null) UnityEngine.Object.Destroy(copy);
            Readable.Clear();
        }

        private static Color Sample(Color[] pixels, int w, int h, float u, float v)
        {
            u = Mathf.Clamp(u, 0f, w - 1);
            v = Mathf.Clamp(v, 0f, h - 1);
            int x0 = Mathf.FloorToInt(u), y0 = Mathf.FloorToInt(v);
            int x1 = Mathf.Min(x0 + 1, w - 1), y1 = Mathf.Min(y0 + 1, h - 1);
            float fx = u - x0, fy = v - y0;
            var top = Color.Lerp(pixels[y0 * w + x0], pixels[y0 * w + x1], fx);
            var bottom = Color.Lerp(pixels[y1 * w + x0], pixels[y1 * w + x1], fx);
            return Color.Lerp(top, bottom, fy);
        }

        /// <summary>Standard alpha-over compositing of <paramref name="s"/> on top of <paramref name="d"/>.</summary>
        private static Color Over(Color s, Color d)
        {
            float a = s.a + d.a * (1f - s.a);
            if (a <= 0f) return Color.clear;
            return new Color(
                (s.r * s.a + d.r * d.a * (1f - s.a)) / a,
                (s.g * s.a + d.g * d.a * (1f - s.a)) / a,
                (s.b * s.a + d.b * d.a * (1f - s.a)) / a,
                a);
        }
    }
}

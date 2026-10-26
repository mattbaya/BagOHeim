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
        private const int Size = 256;
        // The one patch on bag.png sits in the middle of its body: centre (128, 184) from the top-left. Nothing else is drawn on
        // the bag, so the chosen icon is the only picture on it.
        private const int PatchCenterX = 128;
        private const int PatchCenterY = Size - 184;
        // The icon is drawn about as wide and tall as the bag's body, with a white outline, like a patch sewn across it.
        private const int PatchBox = 92;
        private const float PatchBorder = 6f;
        // "Icon only": the game's icon fills the whole slot instead of sitting on a backpack.
        private const int IconOnlyBox = 236;
        private const float IconOnlyBorder = 7f;

        private static Color[] _bag;
        private static readonly Dictionary<string, Sprite> ByPrefab = new Dictionary<string, Sprite>();

        /// <summary>The finished pack icon for an item or piece name (the chosen or default icon), made once and remembered.</summary>
        public static Sprite ForPrefab(string prefab)
        {
            if (string.IsNullOrEmpty(prefab)) return null;
            if (ByPrefab.TryGetValue(prefab, out var cached) && cached != null) return cached;

            var game = GameSprite(prefab);
            if (game == null) return null;

            var composed = Compose(game) ?? game;
            ReleaseCopies();
            ByPrefab[prefab] = composed;
            return composed;
        }

        /// <summary>The game's own icon for an item or a build piece.</summary>
        public static Sprite GameSprite(string prefab)
        {
            var item = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            var drop = item != null ? item.GetComponent<ItemDrop>() : null;
            if (drop != null && drop.m_itemData.m_shared.m_icons != null && drop.m_itemData.m_shared.m_icons.Length > 0)
                return drop.m_itemData.m_shared.m_icons[0];

            var piece = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefab) : null;
            var component = piece != null ? piece.GetComponent<Piece>() : null;
            return component != null ? component.m_icon : null;
        }
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

                bool iconOnly = BagOHeimPlugin.IconStyle.Value == PackIconStyle.IconOnly;
                var pixels = iconOnly ? new Color[Size * Size] : (Color[])bag.Clone();
                int box = iconOnly ? IconOnlyBox : PatchBox;
                float border = iconOnly ? IconOnlyBorder : PatchBorder;
                int centerX = iconOnly ? Size / 2 : PatchCenterX;
                int centerY = iconOnly ? Size / 2 : PatchCenterY;

                float scale = Mathf.Min((float)box / sw, (float)box / sh);
                int dw = Mathf.Max(1, Mathf.RoundToInt(sw * scale));
                int dh = Mathf.Max(1, Mathf.RoundToInt(sh * scale));
                int ox = centerX - dw / 2;
                int oy = centerY - dh / 2;

                // The icon at its new size.
                var icon = new Color[dw * dh];
                for (int y = 0; y < dh; y++)
                    for (int x = 0; x < dw; x++)
                        icon[y * dw + x] = Sample(src, sw, sh, (x + 0.5f) / dw * sw - 0.5f, (y + 0.5f) / dh * sh - 0.5f);

                // A clean white outline: each pixel's coverage comes from its distance to the icon's shape, so the edge is crisp but
                // smooth (not stepped or fuzzy).
                int reach = Mathf.CeilToInt(border) + 1;
                for (int y = -reach; y < dh + reach; y++)
                {
                    for (int x = -reach; x < dw + reach; x++)
                    {
                        int px = ox + x, py = oy + y;
                        if (px < 0 || px >= Size || py < 0 || py >= Size) continue;
                        float distance = DistanceToIcon(icon, dw, dh, x, y, reach);
                        float coverage = Mathf.Clamp01(border + 0.5f - distance);
                        if (coverage <= 0f) continue;
                        pixels[py * Size + px] = Over(new Color(1f, 1f, 1f, coverage), pixels[py * Size + px]);
                    }
                }

                for (int y = 0; y < dh; y++)
                {
                    for (int x = 0; x < dw; x++)
                    {
                        int px = ox + x, py = oy + y;
                        if (px < 0 || px >= Size || py < 0 || py >= Size) continue;
                        pixels[py * Size + px] = Over(icon[y * dw + x], pixels[py * Size + px]);
                    }
                }

                // Mipmaps and trilinear filtering keep it smooth when the slot shows it smaller than it is.
                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
                texture.SetPixels(pixels);
                texture.filterMode = FilterMode.Trilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.Apply(true);
                return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
            }
            catch (Exception e)
            {
                BagOHeimPlugin.Log.LogWarning($"Couldn't put an icon on the backpack; using the plain game icon. {e.Message}");
                return null;
            }
        }

        /// <summary>The distance from (x, y) to the nearest solid pixel of the icon (a large number if none is within <paramref name="reach"/>).</summary>
        private static float DistanceToIcon(Color[] icon, int w, int h, int x, int y, int reach)
        {
            float best = float.MaxValue;
            for (int dy = -reach; dy <= reach; dy++)
            {
                int sy = y + dy;
                if (sy < 0 || sy >= h) continue;
                for (int dx = -reach; dx <= reach; dx++)
                {
                    int sx = x + dx;
                    if (sx < 0 || sx >= w) continue;
                    if (icon[sy * w + sx].a < 0.5f) continue;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d < best) best = d;
                }
            }

            return best;
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

using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace BagOHeim
{
    /// <summary>
    /// The panel beside an open pack: name, refill amounts (edit, add, remove),
    /// "Remember these amounts" and "Refill".
    /// </summary>
    internal static class PackPanel
    {
        private const float Width = 420f;
        // Everything in the panel is drawn this much bigger than its basic layout, so the text is easy to read.
        private const float ReadableZoom = 1.3f;
        // The gap between the pack's contents window and this panel (before scaling).
        private const float GapToContents = 6f;
        private const float RowHeight = 34f;
        private const int MaxTargetRows = 8;
        private const int MaxResults = 5;

        private static GameObject _panel;
        private static InputField _nameField;
        private static InputField _searchField;
        private static string _search = "";
        private static string _typedName;
        private static bool _blocking;

        public static void Show()
        {
            _search = "";
            _typedName = null;
            Rebuild();
        }

        /// <summary>
        /// The panel is drawn in fixed pixel sizes on the mod's own canvas, but the game scales its windows with the screen
        /// resolution and the GUI scale setting. Without matching them, the panel looks tiny next to the inventory on a
        /// large or high-resolution screen. This scales it to match (or by the PanelScale setting), keeping it on screen.
        /// </summary>
        private static void MatchGameUiScale(GameObject panel, float height)
        {
            var rect = panel.GetComponent<RectTransform>();
            if (rect == null) return;

            float scale = BagOHeimPlugin.PanelScale.Value;
            if (scale <= 0f)
            {
                float ours = CanvasScale(GUIManager.CustomGUIFront);
                float game = InventoryGui.instance != null ? CanvasScale(InventoryGui.instance.gameObject) : 0f;
                scale = (ours > 0f && game > 0f ? game / ours : 1f) * ReadableZoom;
            }

            // Never taller than the screen (the canvas height is in the same units as the panel).
            var parent = GUIManager.CustomGUIFront != null ? GUIManager.CustomGUIFront.GetComponent<RectTransform>() : null;
            if (parent != null && parent.rect.height > 0f) scale = Mathf.Min(scale, parent.rect.height * 0.95f / height);

            scale = Mathf.Clamp(scale, 0.5f, 4f);
            rect.localScale = Vector3.one * scale;

            // Sit right beside the pack's contents window, as if it were part of it.
            var contents = InventoryGui.instance != null ? InventoryGui.instance.m_container : null;
            if (contents != null && contents.gameObject.activeInHierarchy)
            {
                var corners = new Vector3[4];
                contents.GetWorldCorners(corners);   // bottom-left, top-left, top-right, bottom-right
                float width = Width * scale, tall = height * scale;
                float x = corners[2].x + GapToContents * scale;
                float top = corners[2].y;
                x = Mathf.Min(x, Screen.width - width - 10f);               // stay on screen
                top = Mathf.Clamp(top, tall + 10f, Screen.height - 10f);
                rect.pivot = new Vector2(0f, 1f);
                rect.position = new Vector3(x, top, rect.position.z);
                return;
            }

            // No contents window to sit beside: grow leftwards from the right edge, where the panel is anchored.
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-30f, 0f);
        }

        private static float CanvasScale(GameObject go)
        {
            var canvas = go != null ? go.GetComponentInParent<Canvas>() : null;
            return canvas != null ? canvas.rootCanvas.scaleFactor : 0f;
        }

        public static void Hide()
        {
            if (_panel != null) Object.Destroy(_panel);
            _panel = null;
            _nameField = null;
            _searchField = null;
            SetBlockInput(false);
        }

        public static void Refresh()
        {
            if (_panel != null) Rebuild();
        }

        /// <summary>Call every frame: keeps game hotkeys from firing while typing in our text boxes.</summary>
        public static void Update()
        {
            bool typing = (_nameField != null && _nameField.isFocused) || (_searchField != null && _searchField.isFocused);
            SetBlockInput(typing);
        }

        public static bool IsTyping => _blocking;

        private static void SetBlockInput(bool block)
        {
            if (block == _blocking) return;
            _blocking = block;
            GUIManager.BlockInput(block);
        }

        private static void Rebuild()
        {
            var pack = PackView.OpenPack;
            var inventory = PackView.OpenInventory;
            if (pack == null || inventory == null || GUIManager.CustomGUIFront == null)
            {
                Hide();
                return;
            }

            if (_nameField != null) _typedName = _nameField.text;
            if (_panel != null) Object.Destroy(_panel);

            var targets = pack.Targets;
            var results = SearchResults(pack.Kind, targets);
            int rows = Mathf.Max(1, Mathf.Min(targets.Count, MaxTargetRows));
            float height = 250f + rows * RowHeight + (results.Count + 1) * RowHeight;

            var gui = GUIManager.Instance;
            _panel = gui.CreateWoodpanel(GUIManager.CustomGUIFront.transform,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-Width / 2 - 30f, 0f), Width, height, false);

            MatchGameUiScale(_panel, height);

            float y = -28f;
            Text(pack.Name, 0f, y, 24, gui.ValheimOrange, Width - 30f, TextAnchor.MiddleCenter);
            y -= 38f;

            // Any pack can be renamed; an empty name goes back to the default.
            {
                var field = gui.CreateInputField(_panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(-55f, y), InputField.ContentType.Standard, "Name this pack", 16, 270f, 30f);
                _nameField = field.GetComponent<InputField>();
                _nameField.characterLimit = 32;
                _nameField.text = _typedName ?? pack.Name;
                Button("Rename", 140f, y, 100f, () =>
                {
                    pack.Name = _nameField.text;
                    _typedName = null;
                    PackView.OpenInventory.m_name = pack.Name;
                    InventoryGui.instance.m_currentContainer.m_name = pack.Name;
                    pack.Save(PackView.OpenInventory);
                    Rebuild();
                });
                y -= 42f;
            }

            Button("Remember these amounts", -95f, y, 210f, () =>
            {
                pack.RememberContents(inventory);
                pack.Save(inventory);
                Rebuild();
            });
            Button("Refill", 120f, y, 150f, () =>
            {
                var player = Player.m_localPlayer;
                var area = BaseArea.For(player.transform.position);
                if (!Refill.One(player, area, pack))
                    Refill.Message(player, area.IsValid ? "Nothing to add." : "Nothing to add. Chests are only used inside your base (near a workbench).");
                Rebuild();
            });
            y -= 46f;

            Text("Refills to:", -Width / 2 + 20f, y, 18, gui.ValheimBeige, 200f, TextAnchor.MiddleLeft);
            y -= RowHeight;

            if (targets.Count == 0)
            {
                Text("Nothing yet: fill the pack and click Remember, or add items below.", 0f, y, 14, Color.gray, Width - 40f, TextAnchor.MiddleCenter);
                y -= RowHeight;
            }

            foreach (var target in targets.Take(MaxTargetRows))
            {
                var shared = Refill.SharedOf(target.Prefab);
                string name = shared != null ? Refill.Name(shared) : target.Prefab;
                int have = Pack.Count(inventory, target.Prefab);
                string color = have >= target.Amount ? "#9ccc65" : "#ffb74d";
                Text($"{name}  <color={color}>{have}/{target.Amount}</color>", -Width / 2 + 20f, y, 16, Color.white, 230f, TextAnchor.MiddleLeft);

                var t = target;
                Button("-", 105f, y, 34f, () => Adjust(pack, t.Prefab, -Step(shared)));
                Button("+", 143f, y, 34f, () => Adjust(pack, t.Prefab, +Step(shared)));
                Button("x", 181f, y, 34f, () => Adjust(pack, t.Prefab, -int.MaxValue / 2));
                y -= RowHeight;
            }
            if (targets.Count > MaxTargetRows)
            {
                Text($"+{targets.Count - MaxTargetRows} more", 0f, y, 14, Color.gray, Width - 40f, TextAnchor.MiddleCenter);
                y -= RowHeight;
            }

            y -= 10f;
            var search = gui.CreateInputField(_panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, y), InputField.ContentType.Standard, "Add an item: type part of a name, press Enter", 16, Width - 50f, 30f);
            _searchField = search.GetComponent<InputField>();
            _searchField.text = _search;
            // Search on Enter (not every keystroke): rebuilding the panel would steal the text box's focus.
            _searchField.onEndEdit.AddListener(text =>
            {
                _search = text;
                Rebuild();
            });
            y -= RowHeight + 4f;

            foreach (var (prefab, label, step) in results)
            {
                var p = prefab;
                var s = step;
                Button($"+ {label}", 0f, y, Width - 50f, () =>
                {
                    Adjust(pack, p, s);
                    _search = "";
                    Rebuild();
                });
                y -= RowHeight;
            }
        }

        /// <summary>Default step: a full stack for ammo/materials, otherwise 1 (Shift = ×10).</summary>
        private static int Step(ItemDrop.ItemData.SharedData shared)
        {
            int step = shared != null && shared.m_maxStackSize >= 50 ? 10 : 1;
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? step * 10 : step;
        }

        private static void Adjust(Pack pack, string prefab, int delta)
        {
            var targets = pack.Targets;
            var target = targets.FirstOrDefault(t => t.Prefab == prefab);
            if (target == null) targets.Add(target = new PackTarget { Prefab = prefab, Amount = 0 });
            target.Amount = Mathf.Max(0, target.Amount + delta);
            pack.Targets = targets; // drops zero amounts
            pack.Save(PackView.OpenInventory);
            Rebuild();
        }

        /// <summary>Items this pack accepts whose name matches the search box.</summary>
        private static List<(string prefab, string label, int step)> SearchResults(PackKind kind, List<PackTarget> targets)
        {
            var results = new List<(string, string, int)>();
            string query = (_search ?? "").Trim();
            if (query.Length < 2 || ObjectDB.instance == null) return results;

            var already = new HashSet<string>(targets.Select(t => t.Prefab));
            foreach (var go in ObjectDB.instance.m_items)
            {
                var drop = go != null ? go.GetComponent<ItemDrop>() : null;
                if (drop == null || already.Contains(go.name)) continue;
                var data = drop.m_itemData;
                if (data.m_shared.m_icons == null || data.m_shared.m_icons.Length == 0) continue; // hidden/internal items

                data.m_dropPrefab = go; // prefab templates don't have this set
                if (!PackKinds.Accepts(kind, data)) continue;

                string label = Refill.Name(data.m_shared);
                if (label.IndexOf(query, System.StringComparison.CurrentCultureIgnoreCase) < 0) continue;
                results.Add((go.name, label, Mathf.Min(data.m_shared.m_maxStackSize, 10)));
                if (results.Count >= MaxResults) break;
            }
            return results;
        }

        private static void Text(string text, float x, float y, int size, Color color, float width, TextAnchor anchor)
        {
            var t = GUIManager.Instance.CreateText(text, _panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(x + (anchor == TextAnchor.MiddleLeft ? width / 2 : 0f), y), GUIManager.Instance.AveriaSerifBold,
                size, color, true, Color.black, width, RowHeight - 2f, false).GetComponent<Text>();
            t.alignment = anchor;
            t.supportRichText = true;
        }

        private static void Button(string label, float x, float y, float width, System.Action onClick)
        {
            var go = GUIManager.Instance.CreateButton(label, _panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(x, y), width, 30f);
            go.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() => onClick());
        }
    }
}

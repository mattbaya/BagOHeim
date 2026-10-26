using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace BagOHeim
{
    /// <summary>
    /// The panel beside an open pack: name, refill amounts (edit, add, remove), "Remember these amounts", "Refill", lock,
    /// auto-pickup and the icon picker. It is a child of the pack's contents window, so it always sits beside it and moves
    /// with it; it can be dragged by any empty part and remembers where you left it. Both it and the contents window have a
    /// close button.
    /// </summary>
    internal static class PackPanel
    {
        private const float Width = 420f;
        // Everything in the panel is drawn this much bigger than its basic layout, so the text is easy to read.
        private const float ReadableZoom = 1.3f;
        // The gap between the pack's contents window and this panel.
        private const float GapToContents = 6f;
        private const float RowHeight = 34f;
        private const int MaxTargetRows = 8;
        private const int MaxResults = 5;
        private const int MaxIconRows = 9;

        // Pieces that can be a pack's icon as well as items.
        private static readonly string[] ExtraIcons = { "portal_wood", "piece_chest", "piece_workbench", "forge", "piece_chest_wood", "piece_chest_private" };
        // A starting list for the icon picker, before anything is typed.
        private static readonly string[] CommonIcons =
        {
            "portal_wood", "piece_chest", "ArrowWood", "MeadHealthMinor", "MeadStaminaMinor", "CookedMeat", "Wood", "FineWood", "Stone",
            "Coal", "Copper", "Tin", "Iron", "BlackMetalScrap", "SurtlingCore", "GreydwarfEye", "LeatherScraps", "DeerHide",
        };

        private static GameObject _panel;
        private static GameObject _containerClose;
        private static InputField _nameField;
        private static InputField _searchField;
        private static string _search = "";
        private static string _typedName;
        private static bool _blocking;
        private static bool _pickingIcon;
        private static string _iconSearch = "";
        private static float _height;
        private static Vector2 _offset;

        public static void Show()
        {
            _search = "";
            _typedName = null;
            _pickingIcon = false;
            _iconSearch = "";
            _offset = new Vector2(BagOHeimPlugin.PanelOffsetX.Value, BagOHeimPlugin.PanelOffsetY.Value);
            Rebuild();
        }

        // ------------------------------------------------------------------ where the panel sits

        private static RectTransform Contents =>
            InventoryGui.instance != null ? InventoryGui.instance.m_container : null;

        private static float Zoom => BagOHeimPlugin.PanelScale.Value > 0f ? BagOHeimPlugin.PanelScale.Value : ReadableZoom;

        /// <summary>
        /// Makes the panel a child of the pack's contents window (so it's always beside it, with the game's own scale), drawn above
        /// everything else. If there's no such window, it sits at the right edge of the screen instead.
        /// </summary>
        private static void Attach()
        {
            if (_panel == null) return;
            var rect = _panel.GetComponent<RectTransform>();
            if (rect == null) return;

            var contents = Contents;
            rect.localScale = Vector3.one * Zoom;
            if (contents != null)
            {
                if (rect.parent != contents) rect.SetParent(contents, false);
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                ApplyPosition();
            }
            else
            {
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(1f, 0.5f);
                rect.anchoredPosition = new Vector2(-30f, 0f);
            }
        }

        private static void ApplyPosition()
        {
            if (_panel == null) return;
            var rect = _panel.GetComponent<RectTransform>();
            if (rect != null && rect.parent == Contents) rect.anchoredPosition = new Vector2(GapToContents, 0f) + _offset;
        }

        private static void SaveOffset()
        {
            BagOHeimPlugin.PanelOffsetX.Value = _offset.x;
            BagOHeimPlugin.PanelOffsetY.Value = _offset.y;
        }

        /// <summary>Called every frame: re-attaches if the contents window wasn't there when the panel was made.</summary>
        private static void EnsureAttached()
        {
            if (_panel == null) return;
            var contents = Contents;
            var rect = _panel.GetComponent<RectTransform>();
            if (contents != null && rect != null && rect.parent != contents) Attach();
        }

        // ------------------------------------------------------------------ closing

        private static void CloseBag()
        {
            if (InventoryGui.instance != null && InventoryGui.instance.m_currentContainer != null)
                InventoryGui.instance.CloseContainer();
        }

        /// <summary>A close button in the corner of the pack's contents window (where "Place stacks" is, which packs don't use).</summary>
        private static void EnsureContainerClose()
        {
            if (_containerClose != null || !PackView.IsOpen || GUIManager.Instance == null) return;
            var contents = Contents;
            if (contents == null) return;

            _containerClose = GUIManager.Instance.CreateButton("X", contents, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -26f), 48f, 34f);
            _containerClose.GetComponent<Button>().onClick.AddListener(CloseBag);
        }

        public static void Hide()
        {
            if (_panel != null) Object.Destroy(_panel);
            if (_containerClose != null) Object.Destroy(_containerClose);
            _panel = null;
            _containerClose = null;
            _nameField = null;
            _searchField = null;
            SetBlockInput(false);
        }

        public static void Refresh()
        {
            if (_panel != null) Rebuild();
        }

        /// <summary>Call every frame: keeps the panel in place and game hotkeys from firing while typing in our text boxes.</summary>
        public static void Update()
        {
            EnsureAttached();
            EnsureContainerClose();
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

        // ------------------------------------------------------------------ building the panel

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
            var icons = _pickingIcon ? IconResults(_iconSearch) : null;
            int rows = Mathf.Max(1, Mathf.Min(targets.Count, MaxTargetRows));
            float height = _pickingIcon
                ? 250f + (icons.Count + 1) * RowHeight
                : 342f + rows * RowHeight + (results.Count + 1) * RowHeight;
            _height = height;

            var gui = GUIManager.Instance;
            _panel = gui.CreateWoodpanel(GUIManager.CustomGUIFront.transform,
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-Width / 2 - 30f, 0f), Width, height, false);

            // Draggable by its background. (It's a child of the contents window, so it's drawn and clicked with the rest of the
            // inventory screen: no canvas of its own.)
            var drag = _panel.AddComponent<PanelDrag>();
            drag.Target = _panel.GetComponent<RectTransform>();
            drag.Moved = delta => { _offset += delta; ApplyPosition(); };
            drag.Dropped = SaveOffset;
            drag.Reset = () => { _offset = Vector2.zero; ApplyPosition(); SaveOffset(); };
            Attach();
            LogOnce();

            // The close button, top right.
            Button("X", Width / 2 - 34f, -24f, 44f, CloseBag);

            if (_pickingIcon)
            {
                BuildIconPicker(pack, icons);
                return;
            }

            float y = -28f;
            Text(pack.Name, 0f, y, 24, gui.ValheimOrange, Width - 100f, TextAnchor.MiddleCenter);
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

            // A locked pack is never used automatically for building, crafting or shooting (or picking things up).
            Button(pack.Locked ? "Unlock this pack" : "Lock this pack", -95f, y, 210f, () =>
            {
                pack.Locked = !pack.Locked;
                pack.Save(inventory);
                Rebuild();
            });
            Button(pack.AutoPickup ? "Auto-pickup: ON" : "Auto-pickup: off", 120f, y, 190f, () =>
            {
                pack.AutoPickup = !pack.AutoPickup;
                pack.Save(inventory);
                Rebuild();
            });
            y -= 46f;

            Button("Choose icon...", -95f, y, 210f, () =>
            {
                _pickingIcon = true;
                _iconSearch = "";
                Rebuild();
            });
            Text(IconLabel(pack), 20f, y, 15, Color.gray, 190f, TextAnchor.MiddleLeft);
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

        // ------------------------------------------------------------------ the icon picker

        private static string IconLabel(Pack pack)
        {
            string prefab = pack.ShownIconPrefab;
            string name = DisplayName(prefab);
            return pack.IconPrefab != null ? "Icon: " + name : "Icon: " + name + " (default)";
        }

        private static string DisplayName(string prefab)
        {
            var shared = Refill.SharedOf(prefab);
            if (shared != null) return Refill.Name(shared);
            var go = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefab) : null;
            var piece = go != null ? go.GetComponent<Piece>() : null;
            return piece != null ? Localization.instance.Localize(piece.m_name) : prefab;
        }

        /// <summary>Items (and a few pieces) with an icon, whose name matches the search; a starting list when nothing is typed.</summary>
        private static List<(string prefab, string label)> IconResults(string query)
        {
            var results = new List<(string, string)>();
            query = (query ?? "").Trim();

            if (query.Length < 2)
            {
                foreach (var prefab in CommonIcons)
                {
                    if (PackIcons.GameSprite(prefab) == null) continue;
                    results.Add((prefab, DisplayName(prefab)));
                    if (results.Count >= MaxIconRows) break;
                }

                return results;
            }

            foreach (var prefab in ExtraIcons)
            {
                string label = DisplayName(prefab);
                if (label.IndexOf(query, System.StringComparison.CurrentCultureIgnoreCase) < 0 || PackIcons.GameSprite(prefab) == null) continue;
                results.Add((prefab, label));
            }

            if (ObjectDB.instance != null)
            {
                foreach (var go in ObjectDB.instance.m_items)
                {
                    if (results.Count >= MaxIconRows) break;
                    var drop = go != null ? go.GetComponent<ItemDrop>() : null;
                    if (drop == null || drop.m_itemData.m_shared.m_icons == null || drop.m_itemData.m_shared.m_icons.Length == 0) continue;
                    if (go.name.StartsWith("BOH_") || results.Any(r => r.Item1 == go.name)) continue;

                    string label = Refill.Name(drop.m_itemData.m_shared);
                    if (label.IndexOf(query, System.StringComparison.CurrentCultureIgnoreCase) < 0) continue;
                    results.Add((go.name, label));
                }
            }

            return results;
        }

        private static void BuildIconPicker(Pack pack, List<(string prefab, string label)> icons)
        {
            var gui = GUIManager.Instance;
            float y = -28f;
            Text("Choose an icon for " + pack.Name, 0f, y, 20, gui.ValheimOrange, Width - 100f, TextAnchor.MiddleCenter);
            y -= 42f;

            var search = gui.CreateInputField(_panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, y), InputField.ContentType.Standard, "Search by name, press Enter", 16, Width - 50f, 30f);
            _searchField = search.GetComponent<InputField>();
            _searchField.text = _iconSearch;
            _searchField.onEndEdit.AddListener(text =>
            {
                _iconSearch = text;
                Rebuild();
            });
            y -= RowHeight + 6f;

            if (icons.Count == 0)
            {
                Text("Nothing matches that.", 0f, y, 16, Color.gray, Width - 40f, TextAnchor.MiddleCenter);
                y -= RowHeight;
            }

            foreach (var (prefab, label) in icons)
            {
                var chosen = prefab;
                var sprite = PackIcons.GameSprite(prefab);
                if (sprite != null) IconImage(sprite, -Width / 2 + 38f, y, 28f);
                Button(label, 28f, y, Width - 112f, () =>
                {
                    pack.IconPrefab = chosen;
                    pack.Save(PackView.OpenInventory);
                    Player.m_localPlayer?.GetInventory().Changed();
                    _pickingIcon = false;
                    Rebuild();
                });
                y -= RowHeight;
            }

            y -= 8f;
            Button("Use the default icon", -95f, y, 210f, () =>
            {
                pack.IconPrefab = null;
                pack.Save(PackView.OpenInventory);
                Player.m_localPlayer?.GetInventory().Changed();
                _pickingIcon = false;
                Rebuild();
            });
            Button("Back", 120f, y, 150f, () =>
            {
                _pickingIcon = false;
                Rebuild();
            });
        }

        private static void IconImage(Sprite sprite, float x, float y, float size)
        {
            var go = new GameObject("BagOHeim_IconRow", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_panel.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(x, y);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        // ------------------------------------------------------------------ the refill list

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

        // ------------------------------------------------------------------ small helpers

        private static void Text(string text, float x, float y, int size, Color color, float width, TextAnchor anchor)
        {
            var t = GUIManager.Instance.CreateText(text, _panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(x + (anchor == TextAnchor.MiddleLeft ? width / 2 : 0f), y), GUIManager.Instance.AveriaSerifBold,
                size, color, true, Color.black, width, RowHeight - 2f, false).GetComponent<Text>();
            t.alignment = anchor;
            t.supportRichText = true;
        }

        private static bool _logged;

        /// <summary>Writes where the panel ended up the first time, so a problem with clicks can be diagnosed from the log.</summary>
        private static void LogOnce()
        {
            if (_logged || _panel == null) return;
            _logged = true;
            var rect = _panel.GetComponent<RectTransform>();
            BagOHeimPlugin.Log.LogInfo($"Pack panel: parent '{(rect.parent != null ? rect.parent.name : "none")}', raycaster found: " +
                $"{_panel.GetComponentInParent<GraphicRaycaster>() != null}, event system: {UnityEngine.EventSystems.EventSystem.current != null}, " +
                $"scale {rect.lossyScale.x:0.00}");
        }

        private static void Button(string label, float x, float y, float width, System.Action onClick)
        {
            var go = GUIManager.Instance.CreateButton(label, _panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(x, y), width, 30f);
            go.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() =>
            {
                BagOHeimPlugin.Log.LogInfo("Pack panel button: " + label);
                onClick();
            });
        }
    }
}

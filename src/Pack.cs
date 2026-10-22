using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace BagOHeim
{
    /// <summary>A refill target: keep this many of an item in the pack.</summary>
    internal class PackTarget
    {
        public string Prefab;
        public int Amount;
    }

    /// <summary>
    /// A pack's contents and settings, stored in the pack item's own custom data so they travel
    /// with it (into chests, gravestones, other players' hands) and save with the inventory it's in.
    /// </summary>
    internal class Pack
    {
        internal const string ItemsKey = "BOH.Items";
        private const string TargetsKey = "BOH.Targets";
        private const string NameKey = "BOH.Name";
        // Cached summary so inventory slots can draw the pack without unpacking it every frame.
        private const string WeightKey = "BOH.Weight";
        private const string MainKey = "BOH.Main";
        private const string HaveKey = "BOH.Have";
        private const string WantKey = "BOH.Want";
        private const string NoTeleportKey = "BOH.NoTeleport";
        private const string LockKey = "BOH.Locked";

        public readonly ItemDrop.ItemData Item;
        public readonly PackKinds.Info Info;

        /// <summary>True if the stored contents couldn't be read; the pack then refuses to write, so nothing is overwritten.</summary>
        public bool Unreadable { get; private set; }

        public Pack(ItemDrop.ItemData item)
        {
            Item = item;
            Info = PackKinds.Of(item);
        }

        public static Pack From(ItemDrop.ItemData item) => PackKinds.IsPack(item) ? new Pack(item) : null;

        public PackKind Kind => Info.Kind;

        /// <summary>A locked pack is never used automatically: not for building or crafting, and not for shooting arrows.</summary>
        public bool Locked
        {
            get => Item.m_customData.ContainsKey(LockKey);
            set
            {
                if (value) Item.m_customData[LockKey] = "1";
                else Item.m_customData.Remove(LockKey);
            }
        }

        public string Name
        {
            get => Item.m_customData.TryGetValue(NameKey, out var n) && n.Length > 0 ? n : Info.Label;
            set
            {
                value = (value ?? "").Trim();
                if (value.Length == 0 || value == Info.Label) Item.m_customData.Remove(NameKey);
                else Item.m_customData[NameKey] = value.Length > 32 ? value.Substring(0, 32) : value;
            }
        }

        // ---------------------------------------------------------------- contents

        /// <summary>The pack's inventory. Changes aren't kept until <see cref="Save"/> is called.</summary>
        public Inventory LoadInventory()
        {
            var inventory = new Inventory(Name, null, BagOHeimPlugin.PackWidth.Value, BagOHeimPlugin.PackHeight.Value);
            if (Item.m_customData.TryGetValue(ItemsKey, out var data) && data.Length > 0)
            {
                try
                {
                    inventory.Load(new ZPackage(Convert.FromBase64String(data)));
                    ReHome(inventory);
                }
                catch (Exception e)
                {
                    Unreadable = true;
                    BagOHeimPlugin.Log.LogError($"Could not read the contents of '{Name}'; leaving it untouched. {e.Message}");
                }
            }
            return inventory;
        }

        /// <summary>
        /// If the pack was made bigger than the current Columns/Rows setting, items outside the grid
        /// would be invisible. Move them into free slots, adding rows if needed (never losing items).
        /// </summary>
        private static void ReHome(Inventory inventory)
        {
            var outside = inventory.m_inventory
                .Where(i => i.m_gridPos.x < 0 || i.m_gridPos.y < 0
                         || i.m_gridPos.x >= inventory.GetWidth() || i.m_gridPos.y >= inventory.GetHeight())
                .ToList();
            foreach (var item in outside)
            {
                Vector2i slot;
                while (!TryFreeSlot(inventory, out slot)) inventory.m_height++;
                item.m_gridPos = slot;
            }
        }

        private static bool TryFreeSlot(Inventory inventory, out Vector2i slot)
        {
            var taken = new HashSet<Vector2i>(inventory.m_inventory.Select(i => i.m_gridPos));
            for (int y = 0; y < inventory.GetHeight(); y++)
                for (int x = 0; x < inventory.GetWidth(); x++)
                {
                    slot = new Vector2i(x, y);
                    if (!taken.Contains(slot)) return true;
                }
            slot = default;
            return false;
        }

        /// <summary>Writes the contents back into the pack item and refreshes its cached summary.</summary>
        public void Save(Inventory inventory)
        {
            if (Unreadable) return; // never overwrite contents we couldn't read
            var pkg = new ZPackage();
            inventory.Save(pkg);
            Item.m_customData[ItemsKey] = Convert.ToBase64String(pkg.GetArray());

            var items = inventory.m_inventory;
            float weight = items.Sum(i => i.GetWeight());
            Item.m_customData[WeightKey] = weight.ToString(CultureInfo.InvariantCulture);

            var main = items.GroupBy(i => i.m_dropPrefab != null ? i.m_dropPrefab.name : "")
                .Where(g => g.Key.Length > 0)
                .OrderByDescending(g => g.Sum(i => i.m_stack))
                .FirstOrDefault();
            if (main != null) Item.m_customData[MainKey] = main.Key;
            else Item.m_customData.Remove(MainKey);

            var targets = Targets;
            int want = targets.Sum(t => t.Amount);
            int have = targets.Sum(t => Count(inventory, t.Prefab, t.Amount));
            Item.m_customData[HaveKey] = have.ToString(CultureInfo.InvariantCulture);
            Item.m_customData[WantKey] = want.ToString(CultureInfo.InvariantCulture);

            if (items.Any(i => !i.m_shared.m_teleportable)) Item.m_customData[NoTeleportKey] = "1";
            else Item.m_customData.Remove(NoTeleportKey);
        }

        /// <summary>How many of an item the pack holds, capped at <paramref name="cap"/>.</summary>
        public static int Count(Inventory inventory, string prefab, int cap = int.MaxValue) =>
            Math.Min(cap, inventory.m_inventory.Where(i => i.m_dropPrefab != null && i.m_dropPrefab.name == prefab).Sum(i => i.m_stack));

        // ---------------------------------------------------------------- cached summary

        public float ContentsWeight =>
            Item.m_customData.TryGetValue(WeightKey, out var s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ? w : 0f;

        public string MainPrefab => Item.m_customData.TryGetValue(MainKey, out var s) ? s : null;

        public int Have => Item.m_customData.TryGetValue(HaveKey, out var s) && int.TryParse(s, out var n) ? n : 0;

        public int Want => Item.m_customData.TryGetValue(WantKey, out var s) && int.TryParse(s, out var n) ? n : 0;

        public bool HoldsNonTeleportable => Item.m_customData.ContainsKey(NoTeleportKey);

        // ---------------------------------------------------------------- targets

        /// <summary>What the pack refills to. A Bag O' Portals defaults to two portals' worth plus wood for a workbench.</summary>
        public List<PackTarget> Targets
        {
            get
            {
                if (Item.m_customData.TryGetValue(TargetsKey, out var s))
                    return Parse(s);
                if (Kind == PackKind.Portals)
                {
                    var preset = PackKinds.PortalCost().Select(c => new PackTarget { Prefab = c.prefab, Amount = c.amount * 2 }).ToList();
                    int wood = BagOHeimPlugin.PortalBagWood.Value;
                    if (wood > 0) preset.Add(new PackTarget { Prefab = PackKinds.WorkbenchWood, Amount = wood });
                    return preset;
                }
                return new List<PackTarget>();
            }
            set
            {
                var clean = value.Where(t => !string.IsNullOrEmpty(t.Prefab) && t.Amount > 0)
                    .GroupBy(t => t.Prefab)
                    .Select(g => new PackTarget { Prefab = g.Key, Amount = g.Sum(t => t.Amount) });
                Item.m_customData[TargetsKey] = string.Join(";", clean.Select(t => $"{t.Prefab}={t.Amount}"));
            }
        }

        /// <summary>Sets the targets to exactly what's in the pack right now.</summary>
        public void RememberContents(Inventory inventory)
        {
            Targets = inventory.m_inventory
                .Where(i => i.m_dropPrefab != null)
                .GroupBy(i => i.m_dropPrefab.name)
                .Select(g => new PackTarget { Prefab = g.Key, Amount = g.Sum(i => i.m_stack) })
                .ToList();
        }

        private static List<PackTarget> Parse(string s)
        {
            var result = new List<PackTarget>();
            foreach (var part in s.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=');
                if (kv.Length == 2 && int.TryParse(kv[1], out int amount) && amount > 0)
                    result.Add(new PackTarget { Prefab = kv[0], Amount = amount });
            }
            return result;
        }
    }
}

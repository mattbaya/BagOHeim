using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BagOHeim
{
    /// <summary>Tops packs up to their remembered amounts from chests in the base.</summary>
    internal static class Refill
    {
        /// <summary>Refills every pack you're carrying. Says what's short, or that you need to be in your base.</summary>
        public static void AllCarried(Player player, bool quiet = false)
        {
            if (player == null) return;
            var area = BaseArea.For(player.transform.position);
            if (!area.IsValid)
            {
                if (!quiet) Message(player, "You need to be in your base (near a workbench) to refill packs.");
                return;
            }

            var packs = player.GetInventory().m_inventory.Select(Pack.From).Where(p => p != null).ToList();
            if (packs.Count == 0)
            {
                if (!quiet) Message(player, "You aren't carrying any packs.");
                return;
            }

            int filled = 0;
            foreach (var pack in packs)
                if (One(player, area, pack, quiet)) filled++;

            if (!quiet) Message(player, filled > 0 ? $"Refilled {filled} pack(s)." : "Your packs are already full.");
        }

        /// <summary>
        /// Refills one pack. Returns true if anything was added. Reports anything still short
        /// (top-left message) unless <paramref name="quiet"/>.
        /// </summary>
        public static bool One(Player player, BaseArea area, Pack pack, bool quiet = false)
        {
            var targets = pack.Targets;
            if (targets.Count == 0) return false;

            var live = PackView.LiveInventoryOf(pack.Item);
            var inventory = live ?? pack.LoadInventory();
            if (pack.Unreadable) return false;
            var missing = new List<string>();
            bool changed = false;

            foreach (var target in targets)
            {
                int need = target.Amount - Pack.Count(inventory, target.Prefab);
                if (need <= 0) continue;

                var shared = SharedOf(target.Prefab);
                if (shared == null) continue; // item no longer exists (e.g. removed mod)

                int room = Room(inventory, target.Prefab, shared.m_maxStackSize);
                ItemDrop.ItemData template = null;
                int got = room > 0 ? ChestSupply.Take(player, area, target.Prefab, Mathf.Min(need, room), out template) : 0;
                if (got > 0)
                {
                    Add(inventory, target.Prefab, template, got);
                    changed = true;
                }

                int shortBy = need - got;
                if (shortBy > 0)
                    missing.Add(room < need ? $"{shortBy} {Name(shared)} (no room)" : $"{shortBy} {Name(shared)}");
            }

            if (changed)
            {
                if (live != null) live.Changed(); // the open view saves itself
                else pack.Save(inventory);
                player.GetInventory().Changed();   // carry weight includes pack contents
            }

            if (!quiet && missing.Count > 0)
                player.Message(MessageHud.MessageType.TopLeft, $"'{pack.Name}' is incomplete. Missing: {string.Join(", ", missing)}");
            return changed;
        }

        /// <summary>How many more of an item fit: top-ups to existing stacks plus empty slots.</summary>
        private static int Room(Inventory inventory, string prefab, int maxStack)
        {
            int topUp = inventory.m_inventory
                .Where(i => i.m_dropPrefab != null && i.m_dropPrefab.name == prefab)
                .Sum(i => Mathf.Max(0, maxStack - i.m_stack));
            return topUp + FreeSlots(inventory).Count * maxStack;
        }

        /// <summary>Adds items by topping up existing stacks first, then filling empty slots.</summary>
        private static void Add(Inventory inventory, string prefab, ItemDrop.ItemData template, int amount)
        {
            int max = template.m_shared.m_maxStackSize;
            foreach (var stack in inventory.m_inventory.Where(i => i.m_dropPrefab != null && i.m_dropPrefab.name == prefab))
            {
                int add = Mathf.Min(amount, max - stack.m_stack);
                if (add <= 0) continue;
                stack.m_stack += add;
                amount -= add;
                if (amount == 0) return;
            }

            foreach (var slot in FreeSlots(inventory))
            {
                var stack = template.Clone();
                // Don't carry over another mod's bookkeeping (e.g. GearHeim's loadout tags).
                foreach (var key in stack.m_customData.Keys.Where(k => k.StartsWith("GearHeim.")).ToList())
                    stack.m_customData.Remove(key);
                stack.m_stack = Mathf.Min(amount, max);
                stack.m_equipped = false;
                stack.m_gridPos = slot;
                inventory.m_inventory.Add(stack);
                amount -= stack.m_stack;
                if (amount == 0) return;
            }
        }

        private static List<Vector2i> FreeSlots(Inventory inventory)
        {
            var taken = new HashSet<Vector2i>(inventory.m_inventory.Select(i => i.m_gridPos));
            var free = new List<Vector2i>();
            for (int y = 0; y < inventory.GetHeight(); y++)
                for (int x = 0; x < inventory.GetWidth(); x++)
                    if (!taken.Contains(new Vector2i(x, y))) free.Add(new Vector2i(x, y));
            return free;
        }

        public static ItemDrop.ItemData.SharedData SharedOf(string prefab)
        {
            var go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            var drop = go != null ? go.GetComponent<ItemDrop>() : null;
            return drop != null ? drop.m_itemData.m_shared : null;
        }

        public static string Name(ItemDrop.ItemData.SharedData shared) => Localization.instance.Localize(shared.m_name);

        public static void Message(Player player, string text)
        {
            if (player != null) player.Message(MessageHud.MessageType.Center, text);
        }
    }
}

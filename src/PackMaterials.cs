using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace BagOHeim
{
    /// <summary>
    /// Lets the game count (and use up) materials that are inside packs you're carrying, when you build or craft, so
    /// you don't have to drag them out first. Checks ("do I have 20 Finewood?") include pack contents, and when the
    /// materials are really used, whatever your inventory can't cover is taken out of the packs.
    /// </summary>
    internal static class PackMaterials
    {
        private struct Entry
        {
            public string Name;
            public int Quality, WorldLevel, Stack;
        }

        // What a pack held the last time we read it, so a pack that hasn't changed isn't parsed again and again.
        private class Cached
        {
            public string Data;
            public List<Entry> Entries;
        }

        private static readonly Dictionary<ItemDrop.ItemData, Cached> Cache = new Dictionary<ItemDrop.ItemData, Cached>();
        private static int _frame = -1;
        private static List<Entry> _all = new List<Entry>();
        public static bool Busy;

        public static bool Enabled => BagOHeimPlugin.PackMaterials.Value;

        private static List<Entry> EntriesOf(ItemDrop.ItemData packItem, Pack pack)
        {
            var live = PackView.LiveInventoryOf(packItem);
            if (live != null) return Read(live);

            packItem.m_customData.TryGetValue(Pack.ItemsKey, out var data);
            data = data ?? "";
            if (Cache.TryGetValue(packItem, out var cached) && cached.Data == data) return cached.Entries;

            var inventory = pack.LoadInventory();
            var entries = pack.Unreadable ? new List<Entry>() : Read(inventory);
            Cache[packItem] = new Cached { Data = data, Entries = entries };
            return entries;
        }

        private static List<Entry> Read(Inventory inventory) =>
            inventory.m_inventory
                .Where(i => i.m_shared != null)
                .Select(i => new Entry { Name = i.m_shared.m_name, Quality = i.m_quality, WorldLevel = i.m_worldLevel, Stack = i.m_stack })
                .ToList();

        private static void Refresh(Player player)
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            _all = new List<Entry>();
            foreach (var item in player.GetInventory().m_inventory)
            {
                var pack = Pack.From(item);
                if (pack != null) _all.AddRange(EntriesOf(item, pack));
            }
        }

        private static bool Matches(Entry e, string name, int quality, bool matchWorld) =>
            (name == null || e.Name == name) && (quality < 0 || quality == e.Quality) && (!matchWorld || e.WorldLevel >= Game.m_worldLevel);

        /// <summary>How many of an item are inside the packs you carry.</summary>
        public static int Count(Player player, string name, int quality, bool matchWorld)
        {
            if (!Enabled || player == null) return 0;
            Refresh(player);
            return _all.Where(e => Matches(e, name, quality, matchWorld)).Sum(e => e.Stack);
        }

        /// <summary>Takes up to <paramref name="amount"/> of an item out of the packs you carry. Returns how many were taken.</summary>
        public static int Take(Player player, string name, int amount, int quality, bool matchWorld)
        {
            if (!Enabled || player == null || amount <= 0) return 0;

            int taken = 0;
            foreach (var packItem in player.GetInventory().m_inventory.ToList())
            {
                if (taken >= amount) break;
                var pack = Pack.From(packItem);
                if (pack == null) continue;

                var live = PackView.LiveInventoryOf(packItem);
                var contents = live ?? pack.LoadInventory();
                if (pack.Unreadable) continue;

                bool changed = false;
                foreach (var stack in contents.m_inventory.ToList())
                {
                    if (taken >= amount) break;
                    if (stack.m_shared == null || stack.m_shared.m_name != name) continue;
                    if (quality >= 0 && stack.m_quality != quality) continue;
                    if (matchWorld && stack.m_worldLevel < Game.m_worldLevel) continue;

                    int take = Mathf.Min(stack.m_stack, amount - taken);
                    contents.RemoveItem(stack, take);
                    taken += take;
                    changed = true;
                }

                if (!changed) continue;
                if (live != null) live.Changed();   // the open view saves itself
                else pack.Save(contents);
            }

            if (taken > 0)
            {
                _frame = -1;
                player.GetInventory().Changed();
            }

            return taken;
        }

        public static bool IsPlayersInventory(Inventory inventory) =>
            Player.m_localPlayer != null && inventory == Player.m_localPlayer.GetInventory();
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.CountItems))]
    internal static class CountItemsPatch
    {
        private static void Postfix(Inventory __instance, string name, int quality, bool matchWorldLevel, ref int __result)
        {
            if (!PackMaterials.Enabled || PackMaterials.Busy || name == null || !PackMaterials.IsPlayersInventory(__instance)) return;
            __result += PackMaterials.Count(Player.m_localPlayer, name, quality, matchWorldLevel);
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.HaveItem), typeof(string), typeof(bool))]
    internal static class HaveItemPatch
    {
        private static void Postfix(Inventory __instance, string name, bool matchWorldLevel, ref bool __result)
        {
            if (__result || !PackMaterials.Enabled || PackMaterials.Busy || name == null || !PackMaterials.IsPlayersInventory(__instance)) return;
            __result = PackMaterials.Count(Player.m_localPlayer, name, -1, matchWorldLevel) > 0;
        }
    }

    /// <summary>When materials are used up, whatever your inventory can't cover comes out of your packs.</summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), typeof(string), typeof(int), typeof(int), typeof(bool))]
    internal static class RemoveMaterialsPatch
    {
        private static void Prefix(Inventory __instance, string name, ref int amount, int itemQuality, bool worldLevelBased)
        {
            if (!PackMaterials.Enabled || PackMaterials.Busy || name == null || amount <= 0 || !PackMaterials.IsPlayersInventory(__instance)) return;

            // What the inventory itself holds (counted directly, not through the patched CountItems).
            int have = 0;
            foreach (var item in __instance.m_inventory)
            {
                if (item.m_shared.m_name == name && (itemQuality < 0 || item.m_quality == itemQuality) && (!worldLevelBased || item.m_worldLevel >= Game.m_worldLevel))
                    have += item.m_stack;
            }

            if (have >= amount) return;

            int taken = PackMaterials.Take(Player.m_localPlayer, name, amount - have, itemQuality, worldLevelBased);
            amount -= taken;
        }
    }
}

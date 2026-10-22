using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace BagOHeim
{
    /// <summary>
    /// Lets bows and crossbows use arrows that are in a pack you're carrying. The game only looks for ammo in your real
    /// inventory, so when you start to draw or shoot and have none there, a stack of matching ammo is taken out of a pack
    /// into your inventory, and the game carries on as normal. Refilling (K, or the Refill button) puts spare ammo back.
    /// </summary>
    internal static class AmmoSupply
    {
        /// <summary>True only while the local player is starting to draw or shoot.</summary>
        public static bool Armed;

        private static bool _busy;

        public static void FindInPacks(Inventory inventory, string ammoName, string matchPrefab, ref ItemDrop.ItemData result)
        {
            if (result != null || !Armed || _busy || !BagOHeimPlugin.AmmoFromPacks.Value) return;

            var player = Player.m_localPlayer;
            if (player == null || inventory != player.GetInventory()) return;

            _busy = true;
            try
            {
                if (TakeOut(player, ammoName, matchPrefab)) result = inventory.GetAmmoItem(ammoName, matchPrefab);
            }
            finally
            {
                _busy = false;
            }
        }

        private static bool IsMatch(ItemDrop.ItemData item, string ammoName, string matchPrefab) =>
            item.m_dropPrefab != null
            && (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo || item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.AmmoNonEquipable)
            && item.m_shared.m_ammoType == ammoName
            && (matchPrefab == null || item.m_dropPrefab.name == matchPrefab);

        /// <summary>Moves a stack of matching ammo from a carried pack into the inventory. Returns true if it did.</summary>
        private static bool TakeOut(Player player, string ammoName, string matchPrefab)
        {
            var inventory = player.GetInventory();
            foreach (var packItem in inventory.m_inventory.ToList())
            {
                var pack = Pack.From(packItem);
                if (pack == null || pack.Locked) continue;

                var live = PackView.LiveInventoryOf(packItem);
                var contents = live ?? pack.LoadInventory();
                if (pack.Unreadable) continue;

                var ammo = contents.m_inventory.FirstOrDefault(i => IsMatch(i, ammoName, matchPrefab));
                if (ammo == null) continue;

                int take = Mathf.Min(ammo.m_stack, ammo.m_shared.m_maxStackSize);
                var moved = ammo.Clone();
                moved.m_stack = take;
                moved.m_equipped = false;
                if (!inventory.CanAddItem(moved, take)) continue;

                contents.RemoveItem(ammo, take);
                if (live != null) live.Changed();   // the open view saves itself
                else pack.Save(contents);

                inventory.AddItem(moved);
                inventory.Changed();
                player.Message(MessageHud.MessageType.TopLeft, $"Took {take} {Refill.Name(moved.m_shared)} from {pack.Name}");
                return true;
            }

            return false;
        }
    }

    /// <summary>Arrows are only fetched from packs at the moment the player starts to draw or shoot.</summary>
    [HarmonyPatch(typeof(Attack), nameof(Attack.StartDraw))]
    internal static class DrawPatch
    {
        private static void Prefix(Humanoid character) => AmmoSupply.Armed = character == Player.m_localPlayer;

        private static void Finalizer() => AmmoSupply.Armed = false;
    }

    [HarmonyPatch(typeof(Attack), nameof(Attack.Start))]
    internal static class ShootPatch
    {
        private static void Prefix(Humanoid character) => AmmoSupply.Armed = character == Player.m_localPlayer;

        private static void Finalizer() => AmmoSupply.Armed = false;
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetAmmoItem))]
    internal static class FindAmmoPatch
    {
        private static void Postfix(Inventory __instance, string ammoName, string matchPrefabName, ref ItemDrop.ItemData __result) =>
            AmmoSupply.FindInPacks(__instance, ammoName, matchPrefabName, ref __result);
    }
}

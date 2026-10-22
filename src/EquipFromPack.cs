using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace BagOHeim
{
    /// <summary>
    /// Right-clicking equipment (a weapon, armour, a shield, arrows, a tool...) in an open pack equips it. The game
    /// only equips things that are in your own inventory, so the item is moved there first, then equipped.
    /// Food and potions are left to the game, which can already use them straight from a pack.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
    internal static class EquipFromPackPatch
    {
        private static bool Prefix(Humanoid __instance, Inventory inventory, ItemDrop.ItemData item)
        {
            if (!BagOHeimPlugin.EquipFromPacks.Value || item == null) return true;

            var player = __instance as Player;
            if (player == null || player != Player.m_localPlayer) return true;

            // Only items in the pack that's open right now.
            if (inventory == null || inventory == player.GetInventory() || inventory != PackView.OpenInventory) return true;
            if (!item.IsEquipable()) return true;

            Equip(player, inventory, item);
            return false;
        }

        private static void Equip(Player player, Inventory pack, ItemDrop.ItemData item)
        {
            var inventory = player.GetInventory();

            // A stack of ammo comes out whole (up to one full stack); gear is a single item.
            int take = Mathf.Min(item.m_stack, item.m_shared.m_maxStackSize);
            var moved = item.Clone();
            moved.m_stack = take;
            moved.m_equipped = false;
            if (!inventory.CanAddItem(moved, take))
            {
                player.Message(MessageHud.MessageType.Center, "No room in your inventory to equip that");
                return;
            }

            // Removing it from the open pack makes the pack save itself.
            pack.RemoveItem(item, take);
            inventory.AddItem(moved);
            inventory.Changed();

            // It may have joined an existing stack (arrows), so find the item that now holds it.
            var target = inventory.ContainsItem(moved)
                ? moved
                : inventory.m_inventory.FirstOrDefault(i => i.m_dropPrefab == moved.m_dropPrefab && i.m_worldLevel == moved.m_worldLevel);
            if (target != null) player.EquipItem(target);
        }
    }
}

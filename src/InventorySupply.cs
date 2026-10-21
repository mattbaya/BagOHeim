using System.Linq;
using UnityEngine;

namespace BagOHeim
{
    /// <summary>Takes items from the player's own inventory to refill a pack, so refilling works away from the base too.</summary>
    internal static class InventorySupply
    {
        /// <summary>
        /// Removes up to <paramref name="amount"/> of an item (by prefab name) from your inventory, never anything you're
        /// wearing or holding, and never other packs. Returns how many were taken, plus a copy of one source stack.
        /// </summary>
        public static int Take(Player player, string prefab, int amount, out ItemDrop.ItemData template)
        {
            template = null;
            if (amount <= 0 || !BagOHeimPlugin.TakeFromInventory.Value) return 0;

            var inventory = player.GetInventory();
            int taken = 0;
            foreach (var item in inventory.m_inventory.ToList())
            {
                if (item.m_dropPrefab == null || item.m_dropPrefab.name != prefab) continue;
                if (item.m_equipped || PackKinds.IsPack(item)) continue;

                int take = Mathf.Min(amount - taken, item.m_stack);
                if (template == null) template = item.Clone();
                inventory.RemoveItem(item, take);
                taken += take;
                if (taken >= amount) break;
            }

            return taken;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BagOHeim
{
    /// <summary>Pulls items out of ordinary chests in the base to refill packs.</summary>
    internal static class ChestSupply
    {
        /// <summary>
        /// Removes up to <paramref name="amount"/> of an item (by prefab name) from chests in the area,
        /// nearest first. Returns how many were taken, plus a copy of one source stack to build new stacks from.
        /// </summary>
        public static int Take(Player player, BaseArea area, string prefab, int amount, out ItemDrop.ItemData template)
        {
            template = null;
            if (amount <= 0) return 0;

            int taken = 0;
            foreach (var container in Sources(player, area))
            {
                var inventory = container.GetInventory();
                if (!inventory.m_inventory.Any(i => Matches(i, prefab))) continue;

                if (!container.m_nview.IsOwner())
                {
                    // Take ownership, then reload from the ZDO so we act on the latest contents we've
                    // received. (A deposit made in the last instant before this could still be missed;
                    // chests someone has open are already skipped, which covers the usual case.)
                    container.m_nview.ClaimOwnership();
                    container.Load();
                }

                foreach (var item in inventory.m_inventory.ToList())
                {
                    if (!Matches(item, prefab)) continue;

                    int take = Mathf.Min(amount - taken, item.m_stack);
                    if (template == null) template = item.Clone();
                    // Fires Inventory.Changed, which makes the (now owned) container save itself.
                    inventory.RemoveItem(item, take);
                    taken += take;
                    if (taken >= amount) return taken;
                }
            }
            return taken;
        }

        private static bool Matches(ItemDrop.ItemData item, string prefab) =>
            item.m_dropPrefab != null && item.m_dropPrefab.name == prefab;

        /// <summary>Chests in the area the player could open themselves, nearest first.</summary>
        private static IEnumerable<Container> Sources(Player player, BaseArea area)
        {
            long playerId = player.GetPlayerID();
            return Object.FindObjectsByType<Container>(FindObjectsSortMode.None)
                .Where(c => IsUsable(c, playerId) && area.Contains(c.transform.position))
                .OrderBy(c => area.DistanceFrom(c.transform.position));
        }

        private static bool IsUsable(Container c, long playerId)
        {
            if (c == null || c.m_nview == null || !c.m_nview.IsValid()) return false;
            if (c.GetInventory() == null || PackView.IsPackContainer(c)) return false;
            if (c.GetComponent<TombStone>() != null) return false;       // leave gravestones alone
            // Ship and cart storage shares the vehicle's network object; claiming it would take
            // physics control away from whoever is sailing or pulling it.
            if (c.m_rootObjectOverride != null) return false;
            if (c.GetComponentInParent<Ship>() != null || c.GetComponentInParent<Vagon>() != null) return false;
            // Someone has it open. Only the owner's local flag is reliable; everyone else reads the ZDO.
            if (c.IsInUse() || c.m_nview.GetZDO().GetInt(ZDOVars.s_inUse) == 1) return false;
            if (!c.CheckAccess(playerId)) return false;                  // someone else's private chest
            if (c.m_checkGuardStone && !PrivateArea.CheckAccess(c.transform.position, 0f, false)) return false;
            return true;
        }
    }
}

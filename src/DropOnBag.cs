using HarmonyLib;
using UnityEngine;

namespace BagOHeim
{
    /// <summary>
    /// Drag an item and let go over a bag, and it goes into that bag (if the bag takes that kind of item and has room), instead
    /// of swapping places with it. Anything else behaves as the game always has.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
    internal static class DropOnBagPatch
    {
        private static bool Prefix(InventoryGui __instance, ItemDrop.ItemData item)
        {
            var dragged = __instance.m_dragItem;
            var source = __instance.m_dragInventory;
            if (__instance.m_dragGo == null || dragged == null || source == null || item == null || item == dragged) return true;

            // Dropped on a pack; a pack dragged onto a pack is left to the game (they swap places).
            if (!PackKinds.IsPack(item) || PackKinds.IsPack(dragged)) return true;

            var player = Player.m_localPlayer;
            if (player == null || player.IsTeleporting()) return true;

            var pack = new Pack(item);
            if (!PackKinds.Accepts(pack.Kind, dragged)) return true;   // not this bag's kind of thing: swap as usual

            var live = PackView.LiveInventoryOf(item);
            if (live != null && live == source) return true;           // out of the open bag onto its own icon: nothing to do
            var contents = live ?? pack.LoadInventory();
            if (pack.Unreadable) return true;

            int amount = Mathf.Clamp(__instance.m_dragAmount, 1, dragged.m_stack);
            var moved = dragged.Clone();
            moved.m_stack = amount;
            moved.m_equipped = false;
            if (!contents.CanAddItem(moved, amount))
            {
                player.Message(MessageHud.MessageType.Center, $"{pack.Name} has no room for that");
                return false;
            }

            // Take it out of where it came from (unequipping it first if it was worn), then into the bag.
            player.RemoveEquipAction(dragged);
            player.UnequipItem(dragged, triggerEquipEffects: false);
            source.RemoveItem(dragged, amount);
            contents.AddItem(moved);
            if (live != null) live.Changed();
            else pack.Save(contents);

            player.GetInventory().Changed();
            __instance.SetupDragItem(null, null, 1);
            __instance.m_moveItemEffects.Create(__instance.transform.position, Quaternion.identity);
            return false;
        }
    }
}

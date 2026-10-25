using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace BagOHeim
{
    /// <summary>
    /// Packs that are set to hold an item (and have Auto-pickup switched on) take that item first when you pick it up, until they
    /// have their amount. It applies to picking things up (by hand or automatically as you walk over them) and never to a locked
    /// pack. Whatever a pack can't take carries on into your inventory as normal.
    /// </summary>
    internal static class BagPickup
    {
        // Greater than zero while the player is picking something up.
        private static int _active;
        private static float _lastMessage;

        public static bool Active => _active > 0;
        public static void Enter() { if (Player.m_localPlayer != null) _active++; }
        public static void Leave() { if (_active > 0) _active--; }

        private static bool Enabled => BagOHeimPlugin.AutoPickupEnabled.Value;

        private class Slot
        {
            public ItemDrop.ItemData PackItem;
            public Pack Pack;
            public Inventory Live;
            public int Room;
        }

        /// <summary>The packs that would take this item right now, with how many each has room for.</summary>
        private static List<Slot> Wanting(Player player, ItemDrop.ItemData incoming)
        {
            var list = new List<Slot>();
            if (!Enabled || incoming == null || incoming.m_dropPrefab == null || PackKinds.IsPack(incoming)) return list;

            string prefab = incoming.m_dropPrefab.name;
            foreach (var packItem in player.GetInventory().m_inventory)
            {
                var pack = Pack.From(packItem);
                if (pack == null || pack.Locked || !pack.AutoPickup) continue;
                if (!PackKinds.Accepts(pack.Kind, incoming)) continue;

                var target = pack.Targets.FirstOrDefault(t => t.Prefab == prefab);
                if (target == null) continue;

                var live = PackView.LiveInventoryOf(packItem);
                var contents = live ?? pack.LoadInventory();
                if (pack.Unreadable) continue;

                int room = target.Amount - Pack.Count(contents, prefab);
                if (room <= 0) continue;
                list.Add(new Slot { PackItem = packItem, Pack = pack, Live = live, Room = room });
            }

            return list;
        }

        /// <summary>True if the packs could take the whole stack (so a full inventory can still pick it up).</summary>
        public static bool CanTakeAll(Player player, ItemDrop.ItemData incoming, int stack)
        {
            if (stack <= 0) stack = incoming != null ? incoming.m_stack : 0;
            return stack > 0 && Wanting(player, incoming).Sum(s => s.Room) >= stack;
        }

        /// <summary>Moves what the packs can take out of <paramref name="incoming"/> into them. Returns how many were taken.</summary>
        public static int Absorb(Player player, ItemDrop.ItemData incoming)
        {
            int taken = 0;
            foreach (var slot in Wanting(player, incoming))
            {
                if (incoming.m_stack <= 0) break;

                var contents = slot.Live ?? slot.Pack.LoadInventory();
                int take = Mathf.Min(slot.Room, incoming.m_stack);
                var moved = incoming.Clone();
                moved.m_stack = take;
                moved.m_equipped = false;
                if (!contents.CanAddItem(moved, take))
                {
                    // Not enough room for the whole amount: fit what does.
                    take = FitInto(contents, incoming, take);
                    if (take <= 0) continue;
                    moved.m_stack = take;
                }

                contents.AddItem(moved);
                if (slot.Live != null) slot.Live.Changed();   // the open view saves itself
                else slot.Pack.Save(contents);

                incoming.m_stack -= take;
                taken += take;
                Say(player, take, incoming, slot.Pack);
            }

            if (taken > 0) player.GetInventory().Changed();
            return taken;
        }

        private static int FitInto(Inventory contents, ItemDrop.ItemData incoming, int want)
        {
            for (int n = want; n > 0; n--)
            {
                var probe = incoming.Clone();
                probe.m_stack = n;
                if (contents.CanAddItem(probe, n)) return n;
            }

            return 0;
        }

        private static void Say(Player player, int amount, ItemDrop.ItemData item, Pack pack)
        {
            if (Time.time - _lastMessage < 0.5f) return;
            _lastMessage = Time.time;
            player.Message(MessageHud.MessageType.TopLeft, $"{amount} {Refill.Name(item.m_shared)} into {pack.Name}");
        }
    }

    [HarmonyPatch]
    internal static class PickupStartPatch
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            // Only the ones that exist in this version of the game.
            var humanoid = AccessTools.Method(typeof(Humanoid), "Pickup");
            if (humanoid != null) yield return humanoid;
            var own = AccessTools.DeclaredMethod(typeof(Player), "Pickup");
            if (own != null) yield return own;
            var auto = AccessTools.Method(typeof(Player), "AutoPickup");
            if (auto != null) yield return auto;
        }

        private static void Prefix(object __instance)
        {
            if (__instance == Player.m_localPlayer) BagPickup.Enter();
        }

        private static void Finalizer(object __instance)
        {
            if (__instance == Player.m_localPlayer) BagPickup.Leave();
        }
    }

    /// <summary>Whatever the packs can take goes in first; the rest carries on.</summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), typeof(ItemDrop.ItemData))]
    internal static class AbsorbPatch
    {
        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (!BagPickup.Active || !PackMaterials.IsPlayersInventory(__instance)) return true;

            BagPickup.Absorb(Player.m_localPlayer, item);
            if (item.m_stack > 0) return true;

            __result = true;
            return false;
        }
    }

    /// <summary>A full inventory can still pick something up if the packs will take all of it.</summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.CanAddItem), typeof(ItemDrop.ItemData), typeof(int))]
    internal static class CanAbsorbPatch
    {
        private static void Postfix(Inventory __instance, ItemDrop.ItemData item, int stack, ref bool __result)
        {
            if (__result || !BagPickup.Active || !PackMaterials.IsPlayersInventory(__instance)) return;
            __result = BagPickup.CanTakeAll(Player.m_localPlayer, item, stack);
        }
    }
}

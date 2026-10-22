using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace BagOHeim
{
    /// <summary>Using a pack (right-click, or its hotbar key) opens it.</summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
    internal static class UseItemPatch
    {
        private static bool Prefix(Humanoid __instance, Inventory inventory, ItemDrop.ItemData item)
        {
            if (!PackKinds.IsPack(item)) return true;
            var player = __instance as Player;
            if (player == null || player != Player.m_localPlayer) return true;
            if (inventory != null && inventory != player.GetInventory()) return true;

            if (PackView.LiveInventoryOf(item) != null) InventoryGui.instance.CloseContainer();
            else PackView.Open(player, new Pack(item));
            return false;
        }
    }

    /// <summary>Only items a pack accepts can go into it (and never another pack).</summary>
    [HarmonyPatch]
    internal static class PackAcceptsPatch
    {
        private static float _lastMessage;

        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData) });
            yield return AccessTools.Method(typeof(Inventory), nameof(Inventory.AddItem),
                new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int), typeof(bool) });
        }

        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (!PackView.IsPackInventory(__instance)) return true;
            var pack = PackView.RegisteredPack;
            if (pack == null || PackKinds.Accepts(pack.Kind, item)) return true;

            NotifyRefused(pack.Kind);
            __result = false;
            return false;
        }

        internal static void NotifyRefused(PackKind kind)
        {
            if (Time.time - _lastMessage < 1f || Player.m_localPlayer == null) return;
            _lastMessage = Time.time;
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, PackKinds.RefusalMessage(kind));
        }
    }

    /// <summary>
    /// Vanilla swaps two occupied slots by removing the dragged item first and then assuming both
    /// moves succeed. If our "only accepted items" rule refused one of them, an item would vanish.
    /// So refuse the whole drag up front whenever either half of a swap isn't allowed.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.DropItem))]
    internal static class PackDropGuardPatch
    {
        private static bool Prefix(InventoryGrid __instance, Inventory fromInventory, ItemDrop.ItemData item, Vector2i pos, ref bool __result)
        {
            var pack = PackView.RegisteredPack;
            if (pack == null) return true;

            var dest = __instance.m_inventory;
            bool intoPack = PackView.IsPackInventory(dest);
            bool outOfPack = PackView.IsPackInventory(fromInventory);
            if (!intoPack && !outOfPack) return true;

            bool refuse = intoPack && !PackKinds.Accepts(pack.Kind, item);
            if (!refuse && outOfPack)
            {
                // The item sitting in the target slot swaps back into the pack.
                var displaced = dest.GetItemAt(pos.x, pos.y);
                refuse = displaced != null && displaced != item && !PackKinds.Accepts(pack.Kind, displaced);
            }
            if (!refuse) return true;

            PackAcceptsPatch.NotifyRefused(pack.Kind);
            __result = false;
            return false;
        }
    }

    /// <summary>Holding Use over a pack would send a "stack all" request nothing answers.</summary>
    [HarmonyPatch(typeof(Container), nameof(Container.StackAll))]
    internal static class PackStackAllPatch
    {
        private static bool Prefix(Container __instance) => !PackView.IsPackContainer(__instance);
    }

    /// <summary>The pack view shares the player's network object; don't flag the player as "in use".</summary>
    [HarmonyPatch(typeof(Container), nameof(Container.SetInUse))]
    internal static class PackSetInUsePatch
    {
        private static bool Prefix(Container __instance) => !PackView.IsPackContainer(__instance);
    }

    /// <summary>A pack weighs its own weight plus its contents at the configured rate.</summary>
    [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetWeight))]
    internal static class PackWeightPatch
    {
        private static void Postfix(ItemDrop.ItemData __instance, ref float __result)
        {
            if (!PackKinds.IsPack(__instance)) return;
            __result += new Pack(__instance).ContentsWeight * BagOHeimPlugin.WeightFactor.Value;
        }
    }

    /// <summary>A pack holding ore (or anything else portals refuse) blocks portals like the item itself would.</summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.IsTeleportable))]
    internal static class PackTeleportPatch
    {
        private static void Postfix(Inventory __instance, bool allowAllItems, ref bool __result)
        {
            if (!__result || allowAllItems) return;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.TeleportAll)) return;
            if (__instance.m_inventory.Any(i => PackKinds.IsPack(i) && new Pack(i).HoldsNonTeleportable))
                __result = false;
        }
    }

    /// <summary>
    /// "Put it in a chest and take it out" refill: a pack that has been stored in a chest
    /// refills when it comes back into your inventory inside your base.
    /// </summary>
    [HarmonyPatch]
    internal static class ChestRoundTripPatch
    {
        private const string StashedKey = "BOH.Stashed";

        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Inventory), nameof(Inventory.MoveItemToThis),
                new[] { typeof(Inventory), typeof(ItemDrop.ItemData) });
            yield return AccessTools.Method(typeof(Inventory), nameof(Inventory.MoveItemToThis),
                new[] { typeof(Inventory), typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int) });
            yield return AccessTools.Method(typeof(Inventory), nameof(Inventory.MoveAll), new[] { typeof(Inventory) });
        }

        private static void Postfix(Inventory __instance)
        {
            var player = Player.m_localPlayer;
            if (player == null || PackView.IsPackInventory(__instance)) return;
            var playerInventory = player.GetInventory();

            var packs = __instance.m_inventory.Where(PackKinds.IsPack).ToList();
            if (packs.Count == 0) return;

            try
            {
                if (__instance != playerInventory)
                {
                    foreach (var item in packs) item.m_customData[StashedKey] = "1";
                    // The container saved before our flag was set; save again so the flag is kept.
                    __instance.Changed();
                    return;
                }

                var area = BaseArea.For(player.transform.position);
                foreach (var item in packs)
                {
                    if (!item.m_customData.Remove(StashedKey)) continue;
                    if (area.IsValid) Refill.One(player, area, new Pack(item));
                }
            }
            catch (System.Exception e)
            {
                // Never let a refill problem break a vanilla item move.
                BagOHeimPlugin.Log.LogError($"Chest round-trip refill failed: {e}");
            }
        }
    }

    /// <summary>Custom packs show their own name in the tooltip.</summary>
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.CreateItemTooltip))]
    internal static class PackTooltipPatch
    {
        private static bool Prefix(InventoryGrid __instance, ItemDrop.ItemData item, UITooltip tooltip)
        {
            if (!PackKinds.IsPack(item)) return true;
            var pack = new Pack(item);
            string topic = pack.Name;
            if (pack.Want > 0) topic += $"  ({pack.Have}/{pack.Want})";
            if (pack.Locked) topic += "  [locked]";
            tooltip.Set(topic, item.GetTooltip(), __instance.m_tooltipAnchor);
            return false;
        }
    }

    /// <summary>
    /// Draws a small icon of a pack's main contents in the corner of its inventory slot, and
    /// shows how full it is in place of the stack count.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateGui))]
    internal static class PackSlotOverlayPatch
    {
        private const string OverlayName = "BagOHeim_Contents";
        private static readonly Dictionary<string, Sprite> IconCache = new Dictionary<string, Sprite>();

        private static void Postfix(InventoryGrid __instance)
        {
            var inventory = __instance.m_inventory;
            if (inventory == null) return;

            foreach (var element in __instance.m_elements)
            {
                var overlay = element.transform.Find(OverlayName);
                if (overlay != null) overlay.gameObject.SetActive(false);
            }

            int width = inventory.GetWidth();
            foreach (var item in inventory.m_inventory)
            {
                if (!PackKinds.IsPack(item)) continue;
                var element = __instance.GetElement(item.m_gridPos.x, item.m_gridPos.y, width);
                if (element == null) continue;

                var pack = new Pack(item);
                var icon = IconFor(pack.MainPrefab);
                if (icon != null)
                {
                    var image = OverlayOn(element);
                    image.sprite = icon;
                    image.gameObject.SetActive(true);
                }

                if (pack.Want > 0)
                {
                    element.m_amount.enabled = true;
                    element.m_amount.text = $"{pack.Have}/{pack.Want}";
                }
                if (pack.HoldsNonTeleportable) element.m_noteleport.enabled = true;
            }
        }

        private static Image OverlayOn(InventoryElement element)
        {
            var existing = element.transform.Find(OverlayName);
            if (existing != null) return existing.GetComponent<Image>();

            var go = new GameObject(OverlayName, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(element.transform, false);
            // Top-right corner, about half the slot.
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(0f, 0f);
            rect.offsetMax = new Vector2(-2f, -2f);
            var image = go.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite IconFor(string prefab)
        {
            if (string.IsNullOrEmpty(prefab)) return null;
            if (IconCache.TryGetValue(prefab, out var cached) && cached != null) return cached;
            var go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            var drop = go != null ? go.GetComponent<ItemDrop>() : null;
            var sprite = drop != null ? drop.m_itemData.GetIcon() : null;
            if (sprite != null) IconCache[prefab] = sprite;
            return sprite;
        }
    }
}

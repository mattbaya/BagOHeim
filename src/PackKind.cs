using System.Collections.Generic;
using System.Linq;

namespace BagOHeim
{
    public enum NamingStyle
    {
        /// <summary>Bag O' Potions, Bag O' Lunch, Bag O' Arrows, Bag O' Portals, Bag O' Stuff.</summary>
        BagO,
        /// <summary>Potion Pack, Lunchbox, Quiver, Portal Bag, Backpack.</summary>
        Plain
    }

    public enum PackKind
    {
        Potions,
        Lunchbox,
        Quiver,
        Portals,
        Backpack
    }

    /// <summary>The five pack types: their item prefab, label, icon and what they accept.</summary>
    internal static class PackKinds
    {
        public class Info
        {
            public PackKind Kind;
            public string Prefab;
            public string BagOLabel;
            public string PlainLabel;
            public string Description;
            /// <summary>Prefab (item or piece) whose in-game icon this pack uses.</summary>
            public string IconPrefab;
            public (string item, int amount)[] Recipe;

            /// <summary>Default name, following the NamingStyle setting.</summary>
            public string Label => BagOHeimPlugin.Naming.Value == NamingStyle.Plain ? PlainLabel : BagOLabel;
        }

        public static readonly Info[] All =
        {
            new Info
            {
                Kind = PackKind.Potions, Prefab = "BOH_PotionPack", BagOLabel = "Bag O' Potions", PlainLabel = "Potion Pack", IconPrefab = "MeadHealthMinor",
                Description = "Holds potions and meads, and refills them to the amounts you set from your base's chests.",
                Recipe = new[] { ("LeatherScraps", 6), ("DeerHide", 2), ("Resin", 2) },
            },
            new Info
            {
                Kind = PackKind.Lunchbox, Prefab = "BOH_Lunchbox", BagOLabel = "Bag O' Lunch", PlainLabel = "Lunchbox", IconPrefab = "CookedMeat",
                Description = "Holds food, and refills it to the amounts you set from your base's chests.",
                Recipe = new[] { ("Wood", 6), ("LeatherScraps", 2) },
            },
            new Info
            {
                Kind = PackKind.Quiver, Prefab = "BOH_Quiver", BagOLabel = "Bag O' Arrows", PlainLabel = "Quiver", IconPrefab = "ArrowWood",
                Description = "Holds arrows and bolts, and refills them to the amounts you set from your base's chests.",
                Recipe = new[] { ("DeerHide", 4), ("LeatherScraps", 4), ("Wood", 2) },
            },
            new Info
            {
                Kind = PackKind.Portals, Prefab = "BOH_PortalBag", BagOLabel = "Bag O' Portals", PlainLabel = "Portal Bag", IconPrefab = "portal_wood",
                Description = "Holds what it takes to build two portals, plus wood for a workbench, and refills it from your inventory and your base's chests.",
                Recipe = new[] { ("DeerHide", 4), ("LeatherScraps", 4), ("GreydwarfEye", 2) },
            },
            new Info
            {
                Kind = PackKind.Backpack, Prefab = "BOH_Backpack", BagOLabel = "Bag O' Stuff", PlainLabel = "Backpack", IconPrefab = "piece_chest",
                Description = "Holds anything. Name it, set its amounts, and it refills from your base's chests.",
                Recipe = new[] { ("DeerHide", 6), ("LeatherScraps", 8) },
            },
        };

        private static readonly Dictionary<string, Info> ByPrefab = All.ToDictionary(i => i.Prefab);

        /// <summary>The pack type of an item, or null if it isn't a pack.</summary>
        public static Info Of(ItemDrop.ItemData item) =>
            item?.m_dropPrefab != null && ByPrefab.TryGetValue(item.m_dropPrefab.name, out var info) ? info : null;

        public static bool IsPack(ItemDrop.ItemData item) => Of(item) != null;

        /// <summary>Whether a pack of this kind accepts an item.</summary>
        public static bool Accepts(PackKind kind, ItemDrop.ItemData item)
        {
            if (item == null || IsPack(item)) return false; // no packs inside packs
            var shared = item.m_shared;
            bool food = shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f;
            switch (kind)
            {
                case PackKind.Quiver:
                    return shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo
                        || shared.m_itemType == ItemDrop.ItemData.ItemType.AmmoNonEquipable;
                case PackKind.Lunchbox:
                    return shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable && food;
                case PackKind.Potions:
                    return shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable && !food;
                case PackKind.Portals:
                    return item.m_dropPrefab != null && (item.m_dropPrefab.name == WorkbenchWood || PortalCost().Any(c => c.prefab == item.m_dropPrefab.name));
                default:
                    return true;
            }
        }

        public static string RefusalMessage(PackKind kind)
        {
            switch (kind)
            {
                case PackKind.Quiver: return "This pack only holds arrows and bolts.";
                case PackKind.Lunchbox: return "This pack only holds food.";
                case PackKind.Potions: return "This pack only holds potions and meads.";
                case PackKind.Portals: return "This pack only holds portal materials and wood.";
                default: return "Packs can't go inside other packs.";
            }
        }

        private const string PortalPrefab = "portal_wood";
        /// <summary>A portal can only be built near a workbench, so the Portal bag also carries wood to build one.</summary>
        public const string WorkbenchWood = "Wood";
        private static List<(string prefab, int amount)> _portalCost;

        /// <summary>Materials for one portal, read from the game's own recipe.</summary>
        public static List<(string prefab, int amount)> PortalCost()
        {
            if (_portalCost != null) return _portalCost;
            var result = new List<(string, int)>();
            var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(PortalPrefab) : null;
            var piece = prefab != null ? prefab.GetComponent<Piece>() : null;
            if (piece == null) return result; // not loaded yet; try again later

            foreach (var req in piece.m_resources)
                if (req?.m_resItem != null && req.m_amount > 0)
                    result.Add((req.m_resItem.name, req.m_amount));
            return _portalCost = result;
        }
    }
}

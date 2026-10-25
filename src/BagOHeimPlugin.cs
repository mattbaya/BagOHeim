using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace BagOHeim
{
    public enum PackIconStyle
    {
        /// <summary>A backpack with the contents' icon on it, big and outlined in white.</summary>
        Backpack,
        /// <summary>Just the contents' icon, outlined in white, filling the slot.</summary>
        IconOnly
    }

    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    // Packs are new items: anyone who sees or holds one (and the server) needs the mod.
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class BagOHeimPlugin : BaseUnityPlugin
    {
        public const string Guid = "mbaya.BagOHeim";
        public const string Name = "Bag O' Heim";
        public const string Version = "1.4.0";

        private const string BasePrefab = "LeatherScraps";
        private const float AutoRefillCheckSeconds = 2f;

        internal static ManualLogSource Log;

        internal static ConfigEntry<RangeMode> Mode;
        internal static ConfigEntry<float> Range;
        internal static ConfigEntry<int> PackWidth;
        internal static ConfigEntry<int> PackHeight;
        internal static ConfigEntry<float> WeightFactor;
        internal static ConfigEntry<NamingStyle> Naming;
        internal static ConfigEntry<KeyboardShortcut> RefillKey;
        internal static ConfigEntry<bool> AutoRefill;
        internal static ConfigEntry<bool> TakeFromInventory;
        internal static ConfigEntry<bool> AmmoFromPacks;
        internal static ConfigEntry<bool> PackMaterials;
        internal static ConfigEntry<PackIconStyle> IconStyle;
        internal static ConfigEntry<float> PanelScale;
        internal static ConfigEntry<bool> ShowContentsIcon;
        internal static ConfigEntry<bool> AutoPickupEnabled;
        internal static ConfigEntry<float> PanelOffsetX;
        internal static ConfigEntry<float> PanelOffsetY;
        internal static ConfigEntry<bool> EquipFromPacks;
        internal static ConfigEntry<int> PortalBagWood;

        private Harmony _harmony;
        private float _nextAutoCheck;
        private bool _wasInBase;

        private void Awake()
        {
            Log = Logger;

            Mode = Config.Bind("General", "RangeMode", RangeMode.Workbench,
                "Which chests packs refill from. Workbench = chests in your base: within build range of the workbench " +
                "you're near (workbenches with overlapping ranges count as one base). Distance = within 'Range' meters of you.");
            Range = Config.Bind("General", "Range", 50f,
                "Used when RangeMode is Distance: how far (in meters) chests can be.");
            PackWidth = Config.Bind("Packs", "Columns", 4,
                new ConfigDescription("Slots across in each pack.", new AcceptableValueRange<int>(1, 8)));
            PackHeight = Config.Bind("Packs", "Rows", 2,
                new ConfigDescription("Rows of slots in each pack.", new AcceptableValueRange<int>(1, 4)));
            WeightFactor = Config.Bind("Packs", "ContentsWeight", 0.5f,
                new ConfigDescription("How much a pack's contents count toward carry weight: 1 = full weight, 0.5 = half, 0 = weightless.",
                    new AcceptableValueRange<float>(0f, 1f)));
            AmmoFromPacks = Config.Bind("Packs", "UseAmmoFromPacks", true,
                "Bows and crossbows use arrows and bolts from packs you're carrying (such as a Bag O' Arrows). When you start to draw or shoot with none in your " +
                "inventory, a stack is taken out of a pack into your inventory. Refilling puts spare ammo back.");
            PackMaterials = Config.Bind("Packs", "UsePackContentsForBuilding", true,
                "Materials inside packs you're carrying count when you build or craft, so you don't have to take them out first " +
                "(a Bag O' Portals can supply the portal's materials). Whatever your inventory can't cover is taken out of the packs.");
            EquipFromPacks = Config.Bind("Packs", "EquipFromPacks", true,
                "Right-click a weapon, armour, arrows or other equipment in an open pack to equip it. It is moved into your inventory first, " +
                "because the game only equips what's in your inventory.");
            IconStyle = Config.Bind("Packs", "IconStyle", PackIconStyle.Backpack,
                "How a pack looks in your inventory. Backpack = a backpack with a big, white-outlined icon of its contents on the front. " +
                "IconOnly = just that icon, outlined in white, filling the slot. Takes effect on next game start.");
            ShowContentsIcon = Config.Bind("Packs", "ShowContentsIcon", false,
                "Also show a small icon of a pack's main contents in the corner of its inventory slot. Off by default, since the pack's own icon already shows what it's for.");
            AutoPickupEnabled = Config.Bind("Packs", "AutoPickup", true,
                "Allow packs to pick up the items they're set to hold. Each pack has its own Auto-pickup switch (off by default); this turns the feature off for all of them.");
            PanelOffsetX = Config.Bind("Packs", "PanelOffsetX", 0f,
                new ConfigDescription("Where you dragged the settings panel to, sideways from its usual spot beside the pack. Saved automatically; double-click the panel to reset.", new AcceptableValueRange<float>(-3000f, 3000f)));
            PanelOffsetY = Config.Bind("Packs", "PanelOffsetY", 0f,
                new ConfigDescription("Where you dragged the settings panel to, up or down from its usual spot. Saved automatically.", new AcceptableValueRange<float>(-3000f, 3000f)));
            PanelScale = Config.Bind("Packs", "PanelScale", 0f,
                new ConfigDescription("Size of the settings panel beside an open pack. 0 = match the game's own windows automatically. " +
                    "Set a number (1 = normal, 1.5 = half as big again) if it still looks too small or too large on your screen.", new AcceptableValueRange<float>(0f, 4f)));
            PortalBagWood = Config.Bind("Packs", "PortalBagWood", 20,
                new ConfigDescription("How much Wood a Bag O' Portals holds, so you can build the workbench a portal needs to be built near (0 = none). " +
                    "Applies to Portal bags you haven't set amounts for yourself.", new AcceptableValueRange<int>(0, 200)));
            Naming = Config.Bind("Packs", "NamingStyle", NamingStyle.BagO,
                "Default pack names. BagO = Bag O' Potions, Bag O' Lunch, Bag O' Arrows, Bag O' Portals, Bag O' Stuff. " +
                "Plain = Potion Pack, Lunchbox, Quiver, Portal Bag, Backpack. Packs you've renamed keep their name. " +
                "Crafting menu names update on next game start.");
            RefillKey = Config.Bind("Refill", "RefillKey", new KeyboardShortcut(KeyCode.K),
                "Refill every pack you're carrying from chests in your base.");
            TakeFromInventory = Config.Bind("Refill", "TakeFromInventory", true,
                "Refilling a pack also uses matching items from your own inventory (never what you're wearing or holding, and never other packs), " +
                "so it works away from your base. Chests in your base are used for whatever is still missing.");
            AutoRefill = Config.Bind("Refill", "AutoRefillOnEnteringBase", false,
                "Refill your packs automatically whenever you walk into your base.");

            PrefabManager.OnVanillaPrefabsAvailable += AddPacks;

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(BagOHeimPlugin).Assembly);
            Log.LogInfo($"{Name} {Version} loaded");
        }

        private void AddPacks()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= AddPacks;

            foreach (var info in PackKinds.All)
            {
                var item = new CustomItem(info.Prefab, BasePrefab, new ItemConfig
                {
                    Name = info.Label,
                    Description = info.Description,
                    CraftingStation = "piece_workbench",
                    Requirements = info.Recipe.Select(r => new RequirementConfig(r.item, r.amount)).ToArray(),
                    Icons = PackIcon(info.IconPrefab),
                });

                var drop = item.ItemDrop;
                if (drop == null)
                {
                    Log.LogError($"Could not create {info.Label}.");
                    continue;
                }

                var shared = drop.m_itemData.m_shared;
                shared.m_itemType = ItemDrop.ItemData.ItemType.Misc;
                shared.m_maxStackSize = 1;   // each pack carries its own contents
                shared.m_maxQuality = 1;
                shared.m_weight = 1f;
                shared.m_teleportable = true; // contents are checked separately
                shared.m_value = 0;

                ItemManager.Instance.AddItem(item);
            }

            PackIcons.ReleaseCopies();
        }

        /// <summary>A backpack with the game's own icon for this pack applied like a patch (or just that icon if it can't be).</summary>
        private static Sprite[] PackIcon(string prefabName)
        {
            var game = GameIcon(prefabName);
            var icon = PackIcons.Compose(game) ?? game;
            return icon != null ? new[] { icon } : null;
        }

        /// <summary>The game's own icon for an item or build piece (nothing is copied; it's read at runtime).</summary>
        private static Sprite GameIcon(string prefabName)
        {
            var prefab = PrefabManager.Instance.GetPrefab(prefabName);
            if (prefab == null)
            {
                Log.LogWarning($"Icon prefab {prefabName} not found; using the default icon.");
                return null;
            }

            var drop = prefab.GetComponent<ItemDrop>();
            if (drop != null) return drop.m_itemData.m_shared.m_icons.FirstOrDefault();

            var piece = prefab.GetComponent<Piece>();
            return piece != null ? piece.m_icon : null;
        }

        private void Update()
        {
            PackView.Update();
            PackPanel.Update();

            var player = Player.m_localPlayer;
            if (player == null) return;

            if (!IsTyping() && RefillKey.Value.IsDown())
                Refill.AllCarried(player);

            if (AutoRefill.Value && Time.time >= _nextAutoCheck)
            {
                _nextAutoCheck = Time.time + AutoRefillCheckSeconds;
                bool inBase = BaseArea.For(player.transform.position).IsValid;
                if (inBase && !_wasInBase) Refill.AllCarried(player, quiet: true);
                _wasInBase = inBase;
            }
        }

        private static bool IsTyping() =>
            PackPanel.IsTyping
            || (Chat.instance != null && Chat.instance.HasFocus())
            || global::Console.IsVisible()
            || TextInput.IsVisible()
            || Menu.IsVisible();

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }
}

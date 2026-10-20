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
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    // Packs are new items: anyone who sees or holds one (and the server) needs the mod.
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class BagOHeimPlugin : BaseUnityPlugin
    {
        public const string Guid = "mbaya.BagOHeim";
        public const string Name = "Bag O' Heim";
        public const string Version = "1.0.0";

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
            Naming = Config.Bind("Packs", "NamingStyle", NamingStyle.BagO,
                "Default pack names. BagO = Bag O' Potions, Bag O' Lunch, Bag O' Arrows, Bag O' Portals, Bag O' Stuff. " +
                "Plain = Potion Pack, Lunchbox, Quiver, Portal Bag, Backpack. Packs you've renamed keep their name. " +
                "Crafting menu names update on next game start.");
            RefillKey = Config.Bind("Refill", "RefillKey", new KeyboardShortcut(KeyCode.K),
                "Refill every pack you're carrying from chests in your base.");
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

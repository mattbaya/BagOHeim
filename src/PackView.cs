using UnityEngine;

namespace BagOHeim
{
    /// <summary>
    /// Opens a pack in the game's normal chest panel, so moving items in and out works exactly
    /// like a chest. Behind the scenes it's a Container on an inactive object attached to the
    /// player: its Awake never runs, so it registers nothing and saves nothing to the world.
    /// Every change is written straight back into the pack item.
    /// </summary>
    internal static class PackView
    {
        private static GameObject _host;
        private static Container _container;
        private static Pack _pack;
        private static Inventory _inventory;

        public static Pack OpenPack => IsOpen ? _pack : null;

        /// <summary>The pack whose inventory is currently registered, even for the frame or two while it closes.</summary>
        public static Pack RegisteredPack => _pack;
        public static Inventory OpenInventory => IsOpen ? _inventory : null;

        public static bool IsOpen =>
            _pack != null && InventoryGui.instance != null && InventoryGui.IsVisible()
            && InventoryGui.instance.m_currentContainer == _container;

        public static bool IsPackContainer(Container c) => c != null && c == _container;

        public static bool IsPackInventory(Inventory inventory) => inventory != null && inventory == _inventory;

        /// <summary>The live inventory if this pack is open right now, otherwise null.</summary>
        public static Inventory LiveInventoryOf(ItemDrop.ItemData packItem) =>
            IsOpen && _pack.Item == packItem ? _inventory : null;

        public static void Open(Player player, Pack pack)
        {
            var gui = InventoryGui.instance;
            if (gui == null || player == null) return;

            // Release whatever is open first (a chest would otherwise stay marked "in use", and a held
            // drag item would stay tied to the old inventory), then save the previous pack.
            if (gui.m_currentContainer != null) gui.CloseContainer();
            Finish();

            var loaded = pack.LoadInventory();
            if (pack.Unreadable)
            {
                player.Message(MessageHud.MessageType.Center, $"'{pack.Name}' couldn't be read, so it was left untouched.");
                return;
            }

            if (_host == null || _host.transform.parent != player.transform)
            {
                if (_host != null) Object.Destroy(_host);
                _host = new GameObject("BagOHeim_PackView");
                _host.SetActive(false); // keep Container.Awake from running
                _host.transform.SetParent(player.transform, false);
                _container = _host.AddComponent<Container>();
            }

            _pack = pack;
            _inventory = loaded;
            _inventory.m_onChanged += OnChanged;

            _container.m_name = pack.Name;
            _container.m_width = _inventory.GetWidth();
            _container.m_height = _inventory.GetHeight();
            _container.m_inventory = _inventory;
            _container.m_nview = player.m_nview; // the player always owns their own network object

            gui.Show(_container);
            gui.m_waitForContainerStack = false; // never auto "stack all" into a pack on open
            PackPanel.Show();
        }

        /// <summary>Call every frame: saves and tidies up once the pack is closed or leaves your inventory.</summary>
        public static void Update()
        {
            if (_pack == null) return;

            var player = Player.m_localPlayer;
            bool stillCarried = player != null && player.GetInventory().m_inventory.Contains(_pack.Item);
            if (IsOpen && !stillCarried)
            {
                // The pack was dropped or moved while open; close its view.
                InventoryGui.instance.CloseContainer();
            }
            if (!IsOpen) Finish();
        }

        private static void OnChanged()
        {
            if (_pack == null) return;
            _pack.Save(_inventory);
            Player.m_localPlayer?.GetInventory().Changed(); // carry weight includes pack contents
            PackPanel.Refresh();
        }

        private static void Finish()
        {
            if (_pack == null) return;
            _pack.Save(_inventory);
            _inventory.m_onChanged -= OnChanged;
            Player.m_localPlayer?.GetInventory().Changed();
            _pack = null;
            _inventory = null;
            PackPanel.Hide();
        }
    }
}

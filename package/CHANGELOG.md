# Changelog

## 1.4.2
- Fix for the settings panel's buttons not responding: the panel no longer has a canvas of its own, which could stop clicks reaching it. It also writes where it ended up and each button press to the log (`Pack panel ...`) to help find any remaining problem.

## 1.4.1
- A new Thunderstore icon: a leather backpack with an embroidered mead patch.

## 1.4.0
- **The settings panel is attached to the pack's contents window.** It always opens right beside it and moves with it, so it no longer starts on the right of the screen and jumps left when clicked.
- **Drag the settings panel** by any empty part to put it where you like; the position is remembered (`Packs / PanelOffsetX, PanelOffsetY`). Double-click an empty part to put it back.
- **Close buttons.** An [X] on the settings panel and one on the pack's contents window close the pack without pressing Escape.
- **Drop an item on a bag.** Drag an item from your inventory onto a pack (in the inventory grid) and it goes inside, if the pack accepts it and has room.
- **Choose a pack's icon.** In the settings panel, *Choose icon...* opens a searchable list of the game's items (with a list of common ones to start). The icon is shown big on the bag with a white border, like an embroidered patch. *Use the default icon* undoes it. The little corner icon of what's inside is off by default (`Packs / ShowContentsIcon`).
- **Auto-pickup per pack.** Turn on *Auto-pickup* for a pack and anything you pick up that is in its list of items goes into that pack first, until it is full. Locked packs are skipped. Turn the feature off with `Packs / AutoPickup`.
- The *Place stacks* button is hidden while a pack is open.

## 1.3.3
- **The pack's settings panel no longer starts at the far right and jumps.** It now moves next to the pack's contents window as soon as that window appears when you open a pack, and stays with it, instead of only after you click something.

## 1.3.2
- **Equipping from an open pack now uses the game's own equip.** Right-clicking gear in a pack moves it into your inventory and equips it the same way as from your inventory (including the equip delay most gear has). If it can't be equipped right then, it goes back into the pack instead of staying out. What happened is written to the log (`Equip from pack: ...`).

## 1.3.1
- Packs can't go inside other packs, and that rule now also covers adding an item to a specific slot (it was already enforced when dragging).

## 1.3.0
- **Bigger, clearer pack icons.** The icon on each pack's backpack is about half again as large and has a white outline, so it's easy to see in a small slot. `Packs / IconStyle = IconOnly` drops the backpack and shows just the outlined icon, filling the slot (restart the game to apply).
- **Lock a pack.** A new **Lock this pack** button in the pack's panel keeps a pack out of building, crafting and shooting: a locked pack's contents are never counted or taken automatically. Locked packs show `[locked]` in their tooltip. Unlock it to use it again.
- **Build and craft straight from your packs.** Materials inside packs you're carrying now count when the game checks whether you can build or craft (a **Bag O' Portals** supplies a portal's materials, and the wood for a workbench), and when you do build, whatever your inventory can't cover is taken out of the packs. You no longer have to drag things out first (`Packs / UsePackContentsForBuilding`).

## 1.2.0
- **Bows and crossbows use the arrows in your pack.** The game only looks for ammo in your inventory, so when you start to draw or shoot with none there, a stack of matching arrows or bolts is taken out of a carried pack (such as a Bag O' Arrows) into your inventory, and you shoot as normal. A message tells you what was taken. Press **K** or click **Refill** to put spare ammo back. Switch it off with `Packs / UseAmmoFromPacks`.
- **The pack's settings panel is bigger and closer.** It now scales with the game's windows (on a large or high-resolution screen it used to look tiny next to the inventory), has larger text, and sits right beside the pack's contents window instead of at the edge of the screen. `Packs / PanelScale` sets a size by hand.
- **Equip straight from a pack.** Right-click a weapon, armour, a shield, arrows or any other equipment in an open pack and it is moved into your inventory and equipped in one step (`Packs / EquipFromPacks`).

## 1.1.0
- The button in an open pack is now simply **Refill** (it was "Refill now"). It tops the pack up to what it's set to hold: for a **Bag O' Portals**, two portals' worth.
- Refill now takes from **your own inventory first**, then from chests in your base for anything still missing, so it works away from your base too. It never takes what you're wearing or holding, and never other packs. Switch it off with `Refill / TakeFromInventory`.
- The **K** key (refill all packs) works the same way.
- A **Bag O' Portals** now also holds **20 Wood**, for the workbench a portal has to be built near (`Packs / PortalBagWood`, 0 for none). Portal bags you've already set amounts for keep their own amounts.

## 1.0.0
- Initial release: Bag O' Potions, Bag O' Lunch, Bag O' Arrows, Bag O' Portals and Bag O' Stuff, with remembered amounts and refilling from your base's chests.

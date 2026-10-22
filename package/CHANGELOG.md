# Changelog

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

# Bag O' Heim

A [BepInEx](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) + [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/) mod for Valheim that adds **packs that always hold the same things**: **Bag O' Potions**, **Bag O' Lunch**, **Bag O' Arrows**, **Bag O' Portals** (everything for two portals), and **Bag O' Stuff** for anything else. Each remembers its amounts and refills from your base's chests.

> Testing build: not yet tried in the game.

## Features

- Ready-made packs that only take their kind of item: **Bag O' Arrows** (arrows and bolts), **Bag O' Lunch** (food), **Bag O' Potions** (potions and meads), **Bag O' Portals** (portal materials, preset to two portals' worth). Plus **Bag O' Stuff**, which takes anything.
- Rename any pack. Prefer plain names (Quiver, Lunchbox, Potion Pack, Portal Bag, Backpack)? Set `NamingStyle` to `Plain`.
- Each pack is a backpack with the game's own icon applied like a patch. Its slot also shows a small icon of what's inside, and how full it is.
- Set what a pack refills to by filling it and choosing **Remember these amounts**, or by editing the item list and amounts in a menu.
- Refill with a hotkey anywhere in your base, or by putting the pack in a base chest and taking it back out. Optionally, packs refill automatically when you enter your base (off by default).
- Contents count toward your carry weight at a reduced rate (configurable).

## Building

Same setup as [GearHeim](https://github.com/mattbaya/GearHeim#building), plus `UnityEngine.ImageConversionModule` and `Unity.TextMeshPro` in `libs/Managed/`: .NET 8 SDK, Valheim's managed assemblies in `libs/Managed/` (or `VALHEIM_MANAGED`), and `Jotunn.dll` in `libs/`. Then `./build.sh`.

## Contributing

Bug reports and pull requests are welcome. Please open an issue first for anything larger than a small fix. Releases to Thunderstore are published by the maintainer.

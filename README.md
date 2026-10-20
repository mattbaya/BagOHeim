# BackPackHeim

A [BepInEx](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) + [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/) mod for Valheim that adds **backpacks that always hold the same things**: a **Potions** pack, a **Lunchbox**, a **Quiver**, and packs you name yourself. Each remembers its amounts and refills from your base's chests.

> 🚧 In development. Nothing to install yet.

## Planned

- Ready-made packs that only take their kind of item: **Quiver** (arrows and bolts), **Lunchbox** (food), **Potions** (potions and meads). Plus custom packs that take anything, named whatever you like.
- Set what a pack refills to by filling it and choosing **Remember these amounts**, or by editing the item list and amounts in a menu.
- Refill with a hotkey anywhere in your base, or by putting the pack in a base chest and taking it back out. Optionally, packs refill automatically when you enter your base (off by default).
- Contents count toward your carry weight at a reduced rate (configurable).

## Building

Same setup as [GearHeim](https://github.com/mattbaya/GearHeim#building): .NET 8 SDK, Valheim's managed assemblies in `libs/Managed/` (or `VALHEIM_MANAGED`), and `Jotunn.dll` in `libs/`. Then `./build.sh`.

## Contributing

Bug reports and pull requests are welcome. Please open an issue first for anything larger than a small fix. Releases to Thunderstore are published by the maintainer.

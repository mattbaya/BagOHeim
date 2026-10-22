# Bag O' Heim

A [BepInEx](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) + [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/) mod for Valheim that adds **packs that always hold the same things**: **Bag O' Potions**, **Bag O' Lunch**, **Bag O' Arrows**, **Bag O' Portals** (everything for two portals, plus wood for a workbench), and **Bag O' Stuff** for anything else. Each remembers its amounts and refills from your inventory and your base's chests. Thunderstore package: `BagOHeim`.

Install: [Svaha-BagOHeim on Thunderstore](https://thunderstore.io/c/valheim/p/Svaha/BagOHeim/), or the Assets zip from a [GitHub release](https://github.com/mattbaya/BagOHeim/releases).

Player-facing docs: [package/README.md](package/README.md). Version history: [package/CHANGELOG.md](package/CHANGELOG.md).

## Features

- Ready-made packs that only take their kind of item: **Bag O' Arrows** (arrows and bolts), **Bag O' Lunch** (food), **Bag O' Potions** (potions and meads), **Bag O' Portals** (portal materials, preset to two portals' worth plus workbench wood). Plus **Bag O' Stuff**, which takes anything. Packs can't go inside packs.
- Rename any pack. Prefer plain names (Quiver, Lunchbox, Potion Pack, Portal Bag, Backpack)? Set `NamingStyle` to `Plain`.
- Each pack is a backpack with the game's own icon applied like a patch, large and outlined in white (or just the outlined icon, `IconStyle = IconOnly`). Its slot also shows a small icon of what's inside, and how full it is.
- Set what a pack refills to by filling it and choosing **Remember these amounts**, or by editing the item list and amounts in the panel beside the open pack.
- **Refill** from the panel, a hotkey, or by putting the pack in a base chest and taking it back out. It uses your own inventory first, then chests in your base. Optionally, packs refill automatically when you enter your base.
- **Bows and crossbows use the ammo in a pack** you're carrying, and you can **equip gear straight from an open pack**.
- **Build and craft from your packs:** materials inside carried packs count when the game checks costs, and are used up when you build.
- **Lock a pack** to keep it out of building, crafting and shooting.
- Contents count toward your carry weight at a reduced rate (configurable).

## Layout

| Path | Purpose |
| --- | --- |
| `src/` | Mod source (`BagOHeim.csproj`, targets `netstandard2.1`) |
| `src/Resources/bag.png` | The backpack picture the icons are drawn on (embedded) |
| `package/` | Thunderstore files: `manifest.json`, `README.md`, `CHANGELOG.md`, `icon.png` |
| `docs/images/` | Screenshots used in the Thunderstore description (served from GitHub) |
| `build.sh` | Builds and zips a Thunderstore package into `dist/` |
| `libs/` | Game assemblies and `Jotunn.dll` to compile against (**git-ignored, never commit**) |

## How it works

A pack is a real item (a Jötunn custom item cloned from Leather scraps) whose contents are the game's own `Inventory` serialized into the item's `m_customData` (`BOH.Items`), with its targets, name and lock stored beside it. Opening a pack shows that inventory through the game's container window (`PackView`), with the settings panel (`PackPanel`, built with Jötunn's GUI manager) placed beside it. `Refill` takes items from `InventorySupply` first and `ChestSupply` (chests in the base area from `BaseArea`) second. `AmmoPatches` fetches ammo from a pack into your inventory when you start to draw or shoot with none; `EquipFromPack` moves gear out of the open pack and equips it through the game's own equip; `PackMaterials` makes `Inventory.CountItems`, `HaveItem` and `RemoveItem` count and use pack contents. `PackIcons` composes each backpack icon at runtime from the game's own artwork.

## Building

1. Install the .NET 8 SDK (or newer).
2. Put Valheim's managed assemblies in `libs/Managed/` (or set `VALHEIM_MANAGED`): `assembly_valheim`, `assembly_guiutils`, `assembly_utils`, `UnityEngine`, `UnityEngine.CoreModule`, `UnityEngine.AssetBundleModule`, `UnityEngine.ImageConversionModule`, `UnityEngine.InputLegacyModule`, `UnityEngine.PhysicsModule`, `UnityEngine.TextRenderingModule`, `UnityEngine.UI`, `UnityEngine.UIModule`, `Unity.TextMeshPro`. No game install? The free dedicated server has them: `steamcmd +@sSteamCmdForcePlatformType windows +force_install_dir ./valheim_server +login anonymous +app_update 896660 +quit`.
3. Put `Jotunn.dll` (from the [JotunnLib NuGet package](https://www.nuget.org/packages/JotunnLib), `lib/net462/`) in `libs/`.
4. Run `./build.sh` → `dist/BagOHeim-<version>.zip`.

## Contributing

Bug reports and pull requests are welcome. Please open an issue first for anything larger than a small fix. Releases to Thunderstore are published by the maintainer.

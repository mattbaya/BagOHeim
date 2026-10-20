# Bag O' Heim

Packs that **always hold the same things**. Tell a pack what it should contain, and it refills itself from the chests in your base.

| Pack | Holds | Made at a workbench from |
| --- | --- | --- |
| **Bag O' Potions** | potions and meads | 6 Leather scraps, 2 Deer hide, 2 Resin |
| **Bag O' Lunch** | food | 6 Wood, 2 Leather scraps |
| **Bag O' Arrows** | arrows and bolts | 4 Deer hide, 4 Leather scraps, 2 Wood |
| **Bag O' Portals** | portal materials, preset to **two portals' worth** | 4 Deer hide, 4 Leather scraps, 2 Greydwarf eyes |
| **Bag O' Stuff** | anything | 6 Deer hide, 8 Leather scraps |

## Using a pack

- **Right-click** a pack in your inventory (or press its hotbar key) to open it. It opens like a chest: drag items in and out.
- Next to it, a panel lets you:
  - **Rename** the pack. Leave the name empty to go back to the default.
  - **Remember these amounts**: fill the pack the way you like it, click this, and that's what it refills to.
  - **Edit amounts** with − / + (hold Shift for ×10) or remove items. Search for items to add, even ones you don't have yet.
  - **Refill now**.
- Each pack looks like a backpack with the game's own icon on it, like an iron-on patch (a mead, cooked meat, an arrow, a portal, a chest). A small icon of what's inside shows in the corner of its slot, along with how full it is (e.g. `18/20`).

## Refilling

Packs refill from **ordinary chests in your base**: the area covered by the workbench you're near, with overlapping workbenches counting as one base.

- Press **K** anywhere in your base to refill every pack you're carrying.
- Or put a pack in a chest in your base and take it out again.
- Optional: turn on `AutoRefillOnEnteringBase` to refill whenever you walk into your base (off by default).

If something can't be filled, you're told what's missing. Packs only take from chests you could open yourself, and skip chests someone has open, gravestones, ships and carts.

## Good to know

- A pack's contents count toward your carry weight at **half weight** by default (configurable from full weight to weightless).
- Packs can't go inside other packs.
- A pack holding ore or anything else portals don't allow blocks portals, just as carrying the item would.
- **Install on the server as well as every client.** Packs are new items, and a server without the mod removes items it doesn't recognise.

## Configuration

`BepInEx/config/mbaya.BagOHeim.cfg`, or Gale's config editor:

| Section | Key | Default | Notes |
| --- | --- | --- | --- |
| General | RangeMode | Workbench | `Workbench` = your base; `Distance` = within `Range` meters |
| General | Range | 50 | Meters, used in Distance mode |
| Packs | NamingStyle | BagO | `Plain` = Potion Pack, Lunchbox, Quiver, Portal Bag, Backpack |
| Packs | Columns / Rows | 4 / 2 | Slots per pack |
| Packs | ContentsWeight | 0.5 | 1 = full weight, 0 = weightless |
| Refill | RefillKey | K | |
| Refill | AutoRefillOnEnteringBase | false | |

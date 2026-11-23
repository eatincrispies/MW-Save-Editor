# NFSMW Save Editor

Save editor for **Need for Speed: Most Wanted (2005)**. Supports PC, PS2, GameCube, Xbox 360, and original Xbox saves.

Lets you edit cash and bounty, jump to any Blacklist rank, add or customize cars, and port your career progress between different versions of the game.

Since this is v1.0.0, back up any saves you actually care about before messing with them. The editor makes a `.bak` the first time it saves over a file, but keeping your own copy never hurts.

## Supported saves

| Version | File to open |
|---|---|
| PC | Your profile in `Documents\NFS Most Wanted\<alias>\<alias>` |
| PS2 | `.psu` (uLaunchELF), `.max`, or `.cbs` |
| GameCube | `.gci` |
| Xbox 360 | CON packages and raw Xenia saves |
| Original Xbox | The save file named after your alias |

## Usage

1. Close the game.
2. Open `NFSMWSaveEditor.exe` and click **File > Open**.
3. Make your changes.
4. Hit **Ctrl+S** to save.

If you need help pulling a save off a console, there are step-by-step instructions for each system under **Help > How to Use** in the app.

## What you can edit

- **Career:** Alias, cash, bounty, current car, and the completion stats shown on the main menu.
- **Blacklist:** Jump to any rank—rivals, races, and milestones adjust automatically.
- **Cars:** Add, delete, and customize cars (parts, paint, hidden vinyls, rims, tint, decals, numbers, gauges, upgrades, Junkman parts, heat, bounty, and impounds).
- **Races:** Career and Quick Race status.
- **Markers:** Every marker type.
- **Rap Sheet:** Pursuit and cost-to-state totals.
- **Options:** Same settings you get in the in-game menus.

Most lists have right-click menus, and you can `Ctrl`- or `Shift`-click to select multiple cars at once.

## Adding cars

**Add Car** has every vehicle in the game—Blacklist bosses, Challenge Series cars, Rog's GTO, Ronnie's Supra, the prologue BMW, and so on. Bosses and presets keep their original setup; everything else spawns stock.

Anything labeled **AI vehicle** or **May crash** wasn't made for the player to drive, so definitely test those on a backup.

**Car Lot** adds hidden cars, cops, and traffic to the in-game shop. Cops and traffic show up for $0 with placeholder names because the game doesn't have shop entries for them.

## Transferring saves to another version

1. Open your old save, go to **File > Export**, and export as JSON or XML.
2. On the version you're moving to, start a new career, finish the prologue, and save.
3. Open that new save in the editor, go to **File > Import**, and load the exported file.
4. Check off whatever you want to bring over and click **Import**.
5. Hit **Ctrl+S**.

Heads up: importing cars wipes and replaces every car in the target save. If a part doesn't exist in that version of the game, it falls back to stock and the editor will let you know.

## Hex editing

For manual edits, the [`hashes`](hashes) folder has the hex values for cars, parts, paints, vinyls, races, markers, and flags, along with their offsets.

Editing by hand will invalidate the save's checksums, so open the file in the editor afterward and press **Ctrl+S** to recalculate them.

## Things to keep in mind

- **Xbox 360:** Real hardware won't load an edited save unless you rehash and resign it (e.g. with Horizon). Xenia doesn't care.
- **Original Xbox:** Saves keep whatever signature type they came with, so a save created in xbased stays valid in xbased. You can't change the alias on Xbox saves.
- **GameCube (Dolphin):** Fully stop the game in Dolphin before editing. Dolphin loads the memory card on boot, and a running game will overwrite your edits.
- **PS2 (PAL):** Some PAL saves use an older layout, so Add Car and the Rap Sheet are disabled for those.
- **PC (modded/shared saves):** If the game refuses to load someone else's save, run **Tools > Match My Game's Car List**. Heavily modded garages still might not line up cleanly.
- **Aliases:** Max 7 alphanumeric characters. Changing your alias on PC also renames the profile folder.
- **Deleting cars:** Works like selling a car without getting the cash—its bounty and Rap Sheet stats stay on your career.

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```sh
dotnet build NFSMWSaveEditor.slnx -c Release

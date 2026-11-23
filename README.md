# NFSMW Save Editor

A save editor for **Need for Speed: Most Wanted (2005)**. It works with saves from the PC, PS2, GameCube, Xbox 360 and original Xbox versions of the game.

You can change your cash and bounty, jump to any Blacklist rank, add and customize cars, and move your progress from one version of the game to another.

## Download

The ready-to-run program is in the [`release/NFSMWSaveEditor-1.0.0`](release/NFSMWSaveEditor-1.0.0) folder. You'll also need the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0), or it won't open.

This is the first release, so please keep a backup of any save you care about. The editor makes a `.bak` copy the first time you save a file, but having your own copy never hurts.

## Supported saves

| Version | What to open |
|---|---|
| PC | Your profile in `Documents\NFS Most Wanted\<alias>\<alias>` |
| PS2 | `.psu` (uLaunchELF), `.max` or `.cbs` |
| GameCube | `.gci` |
| Xbox 360 | CON packages and raw Xenia saves |
| Original Xbox | The save file named after your alias |

## How to use it

1. Close the game.
2. Open `NFSMWSaveEditor.exe` and go to **File > Open**.
3. Change whatever you want.
4. Press **Ctrl+S**.

Not sure how to get a save off your console? **Help > How to Use** has steps for each one.

## What you can edit

- **Career:** alias, cash, bounty and current car, plus the same completion stats the game shows on its main menu
- **Blacklist:** jump to any rank, and your rivals, races and milestones follow along
- **Cars:** add, delete and customize cars, including parts, paint, hidden vinyls, rims, tint, decals, numbers, gauges, upgrades, Junkman parts, heat, bounty and impounds
- **Races:** career and Quick Race status
- **Markers:** every marker type
- **Rap Sheet:** pursuit and cost to state totals
- **Options:** the same settings as the game's menus

Most lists have a right-click menu. Hold Ctrl or Shift to select more than one car.

## Adding cars

**Add Car** lists every vehicle in the game, including the Blacklist bosses, Challenge Series cars, Rog's GTO, Ronnie's Supra and the BMW from the prologue. Bosses and presets keep their real look, and everything else comes stock.

Anything marked **AI vehicle** or **May crash** wasn't made for the player, so keep a backup if you try one.

**Car Lot** puts hidden cars, cops and traffic in the game's shop. Cops and traffic show up for $0 with a placeholder name because the game has no shop info for them.

## Moving your progress to another version

1. Open your old save and go to **File > Export**, then pick JSON or XML.
2. In the other version of the game, start a career, beat the prologue and save.
3. Open that save and go to **File > Import**, then pick the same format.
4. Tick what you want to bring over and press **Import**.
5. Press **Ctrl+S**.

Importing cars replaces every car in that save. If a part doesn't exist in the new version, it goes back to stock and the editor tells you.

## Hex editing

If you'd rather do things by hand, the [`hashes`](hashes) folder lists the hex values for cars, parts, paints, vinyls, races, markers and flags, plus where they live in the save. Start with `Read Me First.txt`.

Hex editing breaks the save's checksums. When you're done, open the file in the editor and press **Ctrl+S** to fix them.

## Things to know

- A real Xbox 360 won't accept an edited save until you rehash and resign it with something like Horizon. Xenia doesn't need this.
- Original Xbox saves keep the kind of signature they came with, so a save made in xbased stays valid in xbased. Their alias can't be changed.
- Stop the game in Dolphin before editing. Dolphin reads the memory card when the game boots, and a running game can write over your changes.
- Some PAL PS2 saves use an older layout, so Add Car and the Rap Sheet are turned off for them.
- If the PC game rejects a save from someone else, try **Tools > Match My Game's Car List**. A heavily modded garage still might not line up.
- Aliases can be up to 7 letters or numbers. Changing one on PC also renames the profile folder.
- Deleting a car works like selling it without the cash. Its bounty and Rap Sheet numbers stay on your career.


## Credits

- Made by eatincrispies.
- Testing/Feedback by Roskler


This is a fan project and isn't affiliated with or endorsed by Electronic Arts. Need for Speed and all car names and logos belong to their owners.

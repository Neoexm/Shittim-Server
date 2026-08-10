---
id: mods
title: Mods
---

Everything under Mods edits the game data rather than the account, so a change here shows up for every account on the server and needs both a server restart and a game relaunch before the client sees it.

At the moment there is one section: **Custom characters**.

## Custom characters

Lists the characters this server has minted, with the id each one got, the donor she was cloned from, and how many files travelled with her zip.

**Add character** opens a file picker for a zip, then a dialog with:

- **Name** - what the student is called in game. Pre-filled from the zip's manifest if it has one, otherwise from the zip's filename.
- **Donor** - the shipped student whose rows the new one is built from. Pre-filled if the manifest names one.
- **Character id** - leave blank to take the next free id in the 10000 block.

Installing writes the clone into every copy of `ExcelDB.db` it can find, which is normally three: the one the server reads, the one the resource loader restores from, and the client's own. A `.premods` backup is taken next to each the first time a mod is installed.

Clicking an installed character opens the editor, which reads the current values straight out of the database and writes back only the fields you changed. Three groups:

- **Character** - dev name, rarity, school, club, squad type, tactic role, weapon type, bullet type, armor type, default and max star grade.
- **Profile** - full name, family and personal name, status message, school year, age, birthday, height, hobby, designer, illustrator, voice actor, weapon name and description, introduction.
- **Stats** - HP, attack, defense and heal at level 1 and 100, crit, dodge, accuracy, range, ammo count and ammo cost.

**Delete character** drops every cloned row from every database copy. Accounts that already own her keep a row pointing at an id that no longer exists.

## What it does not do

Art and audio in the zip are copied into `Data/Mods/<id>/` next to the registry and are reported as staged, not installed. A custom character draws the donor's art until either someone repacks the donor's bundles by hand or the mod ships a Unity bundle of its own - in the second case the server rewrites the client's addressable index on the way out and the new names resolve. Both of those happen outside this window; [ship the art under her own name](../modding/own-assets.md) is the walkthrough.

The full walkthrough, the zip format and the artwork side are in [Making a custom student](../modding/index.md).

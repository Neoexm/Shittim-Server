---
id: commands
title: Console commands
---

The server has an in-process command console. Start it with `--console`, optionally with `--id <serverId>` to pick which account the commands act on (default 2). The same commands run through the admin API's `POST /api/admin/command` with `{ uid, command }`, which is how the Control Center's bulk-grant buttons work.

Commands are written with a leading `!` in the console. Through the API, send them without it.

`!help` lists everything. `!<command> help` prints that command's arguments with their descriptions.

## Reference

| Command | Usage | What it does |
| --- | --- | --- |
| `help` | `!help [command]` | list commands, or explain one |
| `give` | `!give <itemname> <amount>` | give items by name, partial name or id. `credits`, `activity` and similar aliases grant whole categories |
| `giveall` | `!giveall` | unlock every playable character |
| `giveequip` | `!giveequip <equipmentname> <tier> <amount>` | equipment by id or name |
| `giveallequip` | `!giveallequip` | every equipment type at every tier |
| `addeleph` | `!addeleph <character> <amount>` | add elephs for a student |
| `checkitem` | `!checkitem <name>` | print an item's details |
| `listitem` | `!listitem <category or search>` | list items |
| `inventory` | `!inventory [add\|remove] [type] [options]` | types: `all`, `characters`, `weapons`, `equipments`, `items`, `gears`, `lobbies`, `scenarios`, `furnitures`, `emblems`. Options: `barebone`, `basic`, `ue30`, `ue50`, `max` |
| `clearinventory` | `!clearinventory` | remove all items and equipment |
| `character` | `!character [add\|remove\|show\|modify] [id or all] [option] [params]` | add options are `barebone`, `basic`, `ue30`, `ue50`, `max`; modify options are `level`, `star`, `skill`, `ps` |
| `max` | `!max <all\|charactername>` | max out a student, or all of them |
| `currency` | `!currency [currencyId or name] [amount]` | `!currency show` prints the balances |
| `mail` | `!mail [type] [id,...] [amount]` | send mail with attachments |
| `gacha` | `!gacha [rate\|guarantee\|reset\|show] [value] [rate]` | e.g. `!gacha rate ssr 30` |
| `setseason` | `!setseason [total\|grand\|drill\|final] [seasonId]` | `show` prints the current ones |
| `setraid` | `!setraid [type] [options] [snapshotid] [battleId]` | content team data |
| `setaccount` | `!setaccount [property] [value]` | change an account property |
| `setting` | `!setting [trackpvp\|usefinal\|bypassteam\|bypasssummon\|changetime] [enable\|disable\|offset]` | `changetime` takes a numeric offset |
| `unlockall` | `!unlockall [campaign\|story\|weekdungeon\|schooldungeon\|battlepass\|mission]` | unlock a content area |
| `unlockbattlepass` | `!unlockbattlepass` | unlock the paid track, keeping the level |
| `accountdata` | `!accountdata <list\|load\|export> <file>` | load and export saved account data |

## Notes on a few of them

**`unlockall campaign` and `unlockall story` are different things.** The client gates campaign and story progression on cleared story records, not on stage rows, so unlocking the stages alone leaves the story locked. The Control Center's unlock button sends both.

**`inventory add items`** is what the "All items" button on the Inventory page runs.

**`max all`** maxes level, skills and stars on everything the account already owns; it does not grant anything.

## Adding a command

Put a class in `Shittim-Server/Commands` deriving from `Command`, tag it with `[CommandHandler(name, hint, usage)]`, declare its arguments as properties with `[Argument(position, regex, description, flags)]`, and implement `Execute()`. The factory discovers it by reflection at startup and `!help` picks it up with no further registration.

Flags are `Optional`, `IgnoreCase` and `Remaining` (the last of which swallows everything from its position onward as one string).

---
id: inventory
title: Inventory
---

Acts on the account selected in the top bar.

## Bulk grants

Five buttons that run the equivalent console command against the target account:

| Button | Command | What it does |
| --- | --- | --- |
| All items | `inventory add items` | one of everything in the item table |
| All equipment | `giveallequip` | every equipment type at every tier |
| All characters | `giveall` | every playable student |
| Max characters | `max all` | maxes level, skills and stars on everything owned |
| Clear inventory | `clearinventory` | removes all items and equipment |

Clear inventory asks for confirmation. It does not touch characters or currencies.

## Items

A searchable list of what the account owns, with an amount next to each. Adding an item opens a picker over the whole item table and asks for a quantity. Removing takes the stack away.

## Characters

The owned students, with their star grade. The stars are clickable - clicking the third star sets the character to three stars.

Granting a character the account already owns does not stack; use the eleph route for that, or the `addeleph` console command.

---
id: editing
title: 'Step 4: edit or delete her'
---

## Editing

Click her in **Mods**, **Custom characters**. Three groups of boxes: the character fields, the profile fields, the stats - the same three lists as [the manifest](package.md). Only boxes you actually changed get sent.

Renaming her rewrites the one shared name row, which is why the new name appears everywhere at once without touching anything else.

Saving needs another server restart.

## Deleting

The delete button drops every row that was cloned for her, from every copy of the database, plus her name row and her staged art folder.

It does not touch accounts. An account that already owns her keeps a row pointing at an id with nothing behind it, which the client handles badly. Take her off the account first.

## Starting over completely

There is a `.premods` copy of each database sitting next to it, from before the first mod was ever installed. Copy those back over all three, delete the mod registry, restart.

For the game's own files, verifying through Steam restores every bundle and the shipped database. That also removes every custom student, since they live in that database.

Next: [how her art works](art-overview.md).

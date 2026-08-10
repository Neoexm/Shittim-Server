---
id: import
title: 'Step 3: install her'
---

Control Center, **Mods**, **Custom characters**, **Add character**. Pick your zip.

The dialog shows what it found. Fill in whatever is missing:

- **Name** - from the manifest, or the zip filename
- **Donor** - from the manifest, or pick one from the list
- **Character id** - leave it blank unless you have a reason

Press **Install**. It takes a while because it is rewriting three 300 MB databases, and the first time it touches each one it copies the whole file to `<name>.db.premods` as a backup.

## What it does while you wait

1. Checks the donor actually exists.
2. Picks an id. Blank means the highest id currently in use in the 10000-19999 student range, plus one, skipping anything already taken. Students live in that range and staying inside it keeps the game's own sanity checks happy.
3. **Checks every copy of the database for that id before writing to any of them.** An id that is free in one copy but taken in another would quietly merge your student into somebody else's rows over there.
4. For each copy, in one all-or-nothing chunk: read the donor's rows, rewrite the ids inside them, apply your overrides, insert.
5. Makes a fresh name row carrying your name in all five languages and points the new student at it.
6. Copies any art out of the zip into the holding folder.
7. Writes the registry entry.

If anything fails partway through, that database is left exactly as it was.

## Restart, relaunch, grant

Three separate things, and skipping any one of them looks like the import failed.

**Restart the server.** It reads the database when it starts up.

**Relaunch the game.** The client reads its own copy when it launches.

**Give her to yourself.** She exists in the game's data but nobody owns her, and the student list only shows students you own. Control Center, Inventory, Characters - or from the console:

```
!character add 10100 max
```

Now she shows up, wearing the donor's face, with your name and bio on her.

Next: [edit or delete her](editing.md), or skip ahead to [how her art works](art-overview.md).

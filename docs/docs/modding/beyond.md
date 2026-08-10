---
id: beyond
title: 'Step 12: editing her tables by hand'
---

Everything from here on is rows in `ExcelDB.db` that no importer and no dialog will write for you. Her skills, her voice, her lines and her MomoTalk are all the same job with different tables, so this page is the mechanics once, and the four after it are what to put in.

## What you already have

The importer clones eighteen tables off the donor and rewrites the ids inside them:

**Core** - `CharacterExcel`, `CostumeExcel`, `CharacterStatExcel`

**Combat** - `CharacterWeaponExcel`, `CharacterGearExcel`, `CharacterTranscendenceExcel`, `CharacterPotentialExcel`, `CharacterPotentialRewardExcel`, `CharacterSkillListExcel`

**Text** - `LocalizeCharProfileExcel`, and a fresh `LocalizeEtcExcel` row for her name

**Relationship and lobby** - `FavorLevelRewardExcel`, `CafeInteractionExcel`, `CharacterAcademyTagsExcel`, `MemoryLobbyExcel`

**Presentation** - `CharacterIllustCoordinateExcel`, `PresetCharacterGroupSettingExcel`, `CharacterDialogExcel`, `CharacterDialogSubtitleExcel`

She levels, fights, sits in the cafe, has a profile and a memory lobby. Note what that list means for the next four steps: her skill *list* was cloned but the skills it points at are still the donor's, and her dialog rows were cloned complete with the donor's words in them. Her voice and her MomoTalk were not cloned at all, so right now she has neither.

## What the tables look like

`ExcelDB.db` is a SQLCipher database. One table per Excel type, one row per record, and the record itself stored as a FlatBuffers blob in a `Bytes` column. The table name is the FlatData type with `Excel` dropped and `DBSchema` added, so `CharacterVoiceExcel` is the table `CharacterVoiceDBSchema`.

That means you cannot edit a field with a SQL `UPDATE`. You read the blob, decode it, build a new one, and write the whole blob back. The [Excel data page](../server/excel-data.md) covers opening the database and the key it wants; the server's own `CustomCharacterService` is a worked example of the read-modify-write, and it is the code the import dialog runs.

The practical shape of every edit below is the same, and it is what the importer does:

1. Read the donor's rows out of the table.
2. Change the id fields so they point at your student instead.
3. Change whatever else you came to change.
4. Insert them back.

Copying a donor row and retargeting it beats building one from nothing, because a shipped row already has correct values in the twenty fields you were not thinking about.

## Three copies

There are three copies of `ExcelDB.db` and a row added to fewer than all three drifts back the next time one gets copied over another. The server reads its dumped copy, the resource loader can restore from the downloaded one, and the client reads its own. The Control Center's [mods page](../control-center/mods.md) lists the paths. Write all three, every time.

## Two things that bite quietly

**The weapon row is not optional.** When the server starts it builds a batch of assist characters, walking every released character and looking up each one's weapon with no fallback. A character with no weapon row throws there and the server dies before it listens, with an error that points at assist-character setup rather than at the character you added. Gear is guarded against being missing. Weapon is not. The importer always clones a weapon row, so this only bites hand edits.

**None of these tables has a uniqueness constraint.** Only plain indexes. An insert that collides with an existing row does not replace it, it duplicates it, and both come back from a lookup with whichever is found first winning. If a change you made does not seem to have taken, the first thing to check is whether you inserted a second row rather than replacing the first.

## Backups

The import dialog copies each database to `<name>.db.premods` the first time it touches it. Hand edits do not, so make your own copy before the first insert. Restoring a 300 MB file is thirty seconds and rebuilding a student is an evening.

## Making her pullable

Not a table you edit here. Banners are Excel data too, and the server's [banner patcher](../server/client-patching.md) is what writes recruitment banners into the client's copy - that is the route to putting her in one, rather than hand-editing the gacha tables. Only worth doing if other people play on your server; on your own, granting her from the console is the same result with none of the work.

Next: [her own skills](skills.md).

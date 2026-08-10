---
id: troubleshooting
title: When it goes wrong
---

## The data half

**The server will not start after an import.** Almost always a missing weapon row, for [the reason here](beyond.md). The importer always clones one, so check for hand edits.

**"Character id N is already in use."** The id is free in one copy of the database and taken in another, or it has leftover rows in some table other than the character table. Leave the id blank and let it pick.

**"No ExcelDB.db could be located."** None of the three paths found a file. Make sure the server has done its resource setup at least once and that the game install was found.

**The import worked but she is not in the game.** Restart the server, relaunch the game, grant her to the account. All three, in that order.

**The import fails on the game's copy.** Blue Archive is running. The launcher counts.

**She vanished after a game update.** An update replaces the game's copy of the database and takes every custom student in it with them. The server's copies still have her, so the server thinks she exists and the client does not, and she shows as a blank entry. Reimport. The same happens after a Steam file verification.

**Her name shows as a raw key.** The name row did not make it, or the character points at a key with no row behind it. Renaming her from the editor rewrites both ends.

**Editing does nothing.** Only changed boxes get sent, and the save still needs a server restart. If one specific field never takes, check it is on one of [the three field lists](package.md) - anything outside them is ignored with no error.

**Deleting left something behind.** Delete drops the database rows and the staged files. It does not touch accounts, and an account holding an id with nothing behind it confuses the client. Remove her from the account first.

## The art half

**Her artwork is scrambled.** Your sheet does not match the layout file's boxes. Read the layout for the rig you are targeting, not one you saw somewhere else.

**A rectangle of face floats over her face.** Your expression plates are opaque crops. They need to be [features only, on transparent](art-overview.md).

**She is upside down.** The 180 degree rotation flag on the head region.

**Parts of one thing bleed into another.** The declared boxes overlap. Stencil each region against the original's transparency.

**Her hair is cut off at the edges.** The rig's geometry only exists where the original had artwork. Grow the stencil a little, or host on a rig whose shape matches her better. The second is the real fix.

**She is invisible after a bundle patch.** You probably created a new texture object instead of replacing the existing one, which broke the internal id the material uses to find it.

**The skeleton will not load after you replaced it.** The layout, the skeleton and the sheet have to share a base name, and the layout names its texture on its first line.

## The rows you added by hand

**An edit did not take.** Two candidates and it is nearly always the first. None of these tables has a uniqueness constraint, so an insert that collided with an existing row duplicated it rather than replacing it, and lookups return whichever they find first. The other one is having written two of the three copies of the database.

**Her skill card still has the donor's name on it.** Her `CharacterSkillListExcel` rows still name the donor's groups. There are four of them and all four need rewriting - the one you missed is usually a combination you cannot reach yet, so it looks fixed until her gear hits tier 2.

**The skill text is right at level 1 and wrong further up.** Every level points at its own localize row. Ten levels, ten rows.

**A skill shows a number instead of a name.** `LocalizeSkillId` on the skill row and `Key` on the localize row do not agree.

**She is still silent.** Voice rows hang off `CharacterVoiceGroupId`, which is her character id - if you copied a donor's rows and forgot that field, you added lines to the donor.

**One line plays nothing.** A path typo. They carry no file extension, so a `.ogg` on the end is a common one, and the clip has to be one that already exists in the client's audio archives.

**Subtitles and audio do not line up.** Subtitle rows key off `LocalizeCVGroup` plus the group id, so a slot name spelled differently on the two rows silently produces a line with no words and words with no line.

**Her voice collection is empty although the lines play in battle.** `CollectionVisible`, or every row gated behind an `UnlockFavorRank` she has not reached.

**MomoTalk has nothing in it.** Group ids not derived from her character id, or every group sitting behind a rank gate. Set one group to open at rank 1 to prove the rest of the wiring first.

**A MomoTalk choice does nothing.** `NextGroupId` pointing at a group that does not exist. Answers fail quietly.

## Starting over

There is a `.premods` copy of each database sitting next to it, from before the first mod was ever installed. Copy those back over all three, delete the mod registry, restart.

For the game's own files, verifying through Steam restores every bundle and the shipped database. That also removes every custom student, since they live in that database.

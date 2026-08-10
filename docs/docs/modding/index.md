---
id: index
title: Making a custom student
---

This section builds one complete student, end to end. Her data, her artwork, her memory lobby, her battle model, her own skills, her voice, the lines she says in the lobby and the cafe, and her MomoTalk. Not a reskin with somebody else's everything sitting behind it.

It is sixteen steps and you do not have to do them in one sitting. She is playable after step 3 and gets more finished from there, so the order below is arranged so that every step you finish is a thing you can go and look at in the game.

## What a finished student is made of

Worth reading once before you start, because a lot of the work later on makes more sense if you already know what it is aiming at.

| | What it is | Where it lives | Step |
| --- | --- | --- | --- |
| Her record | name, school, rarity, stats, profile text | `ExcelDB.db` | 2 |
| Menu artwork | the sprite you see in the student list and on her profile | a Unity asset bundle | 5-9 |
| Memory lobby | the full-screen scene you can talk to her in | a second Spine rig plus a prefab | 10 |
| Battle model | the 3D character, her gun, thirty-four animation clips | a third bundle | 11 |
| Skills | four skills plus the passives, ten levels each | `ExcelDB.db` | 13 |
| Voice | forty-odd lines, and subtitles for the ones with words | `ExcelDB.db`, and the client's audio archives | 14 |
| Her lines | what she says in the lobby, the cafe and on the title screen | `ExcelDB.db` | 15 |
| MomoTalk | a branching chat script tied to her favor rank | `ExcelDB.db` | 16 |

Everything in that table exists for every shipped student. The importer gives you a working version of about half of it on day one by copying it off a donor, which is why this is a build rather than a from-scratch write.

## The two halves

**Her data** lives in a database called `ExcelDB.db`. That is her name, her school, her rarity, her stats, her bio, her skills, her voice lines, what she says in the cafe. It is a normal database and this server can read and write it, so this half is entirely in your hands.

**Her art** lives in Unity asset bundles - packed archives of textures and models that the game loads by name. That half is harder, because the game will only load a name it already knows about.

## How the game decides a name exists

There is a file called `catalog_Remote.bytes`, about 64 MB, that lives in `%USERPROFILE%\AppData\LocalLow\NEXON Games\Blue Archive\`. It is a giant index: every asset the game can load, which bundle it lives in, and where to get that bundle. If a name is not in the index the game cannot load it, no matter what you put on your hard drive or what you call it.

The client only downloads a fresh copy of that index when the server it logged into tells it where to get one. Point that at Shittim and the server hands back the index with your entries added to it, which is how a custom student can have art under her own name rather than wearing the donor's.

So there are two ways to give her art, and they are both fine.

**Borrow the donor's names** and replace the pictures behind them. Needs no extra tools and no index at all, and it looks identical in game. Steps 5 to 8 do this.

**Ship your own names**, in your own bundle, and let the server put them in the index. Costs you installing Unity once, after which every file you add is a file you drop in a folder rather than surgery on somebody else's archive. [Step 9](own-assets.md) walks through it.

Do the first one first even if you intend to end up at the second, because it gets art on screen today and tells you your student is otherwise working. Steps 10 and 11 need the second one, so you will end up there anyway.

## Two warnings before you start

**Close the game.** The client keeps a lock on its own copy of `ExcelDB.db` the whole time it is running, and half of what follows writes to that file.

**Do this on your own server only.** The retail client ships with anti-cheat. Patched game files on a live account is a plausible ban and a hard one to appeal. Playing against your own server sidesteps that entirely, and it is the only setup any of this has been tested on.

## The build order

**Get her into the game.** About ten minutes, and at the end of it she is a real student in your student list wearing the donor's face.

1. [Pick a donor](donor.md) - who she gets copied from, and why that choice decides how good the art can look later
2. [Write her data](package.md) - her name, her school, her stats, her profile, and the file you put them in
3. [Install her](import.md) - the import, the restart, and giving her to yourself
4. [Edit or delete her](editing.md) - because the first pass at her stats is never the last

**Give her a face.** The longest stretch, and the one that decides whether she looks like she belongs.

5. [How her art works](art-overview.md) - the sprite sheet, the pieces, and the three mistakes everyone makes
6. [Make the artwork](artwork.md) - every piece you need
7. [Build the sheet](atlas.md) - compositing into the rig, or building your own
8. [Put it in the game](packing.md) - repacking bundles and making the icons
9. [Ship the art under her own name](own-assets.md) - the same result out of a bundle of your own, and the foundation for the next two

**Give her a body.** Two more rigs, in the order they are worth doing.

10. [The memory lobby](lobby.md) - one more Spine rig and a prefab
11. [The battle model](battle.md) - the 3D character, thirty-four clips, and why that is fewer than it sounds

**Make her hers.** Everything up to here she inherited from the donor. This is where she stops being a copy.

12. [Editing her tables by hand](beyond.md) - what the importer cloned, what it did not, and how to add a row yourself
13. [Her own skills](skills.md) - four skills, ten levels each, and the text that describes them
14. [Her voice](voice.md) - forty-odd lines, the subtitles, and where the audio actually lives
15. [Her own lines](lines.md) - the lobby, the cafe, the title screen and the moment you first get her
16. [Her MomoTalk](momotalk.md) - a branching script, favor gates and the schedule dates

And when something does not appear: [when it goes wrong](troubleshooting.md).

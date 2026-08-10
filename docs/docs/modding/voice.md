---
id: voice
title: 'Step 14: her voice'
---

The importer does not clone voice rows, so at the moment your student is silent - no lines when she is picked, nothing when she fires her EX, an empty voice collection on her profile. This step fills that in.

## What a voice actually is

Two tables and a pile of audio files, and they are more separable than you would expect.

`CharacterVoiceExcel` is the list of lines. One row per line, all of them tagged with `CharacterVoiceGroupId`, which is just her character id. Aru has 46 rows out of the table's 9,602, spread across 271 groups - roughly one group per voiced character.

`CharacterVoiceSubtitleExcel` is the words. 7,209 rows, 33 of them Aru's, which is fewer than her 46 lines because grunts and shouts do not get subtitles.

The audio itself is somewhere else entirely, and that is the part worth understanding before you start writing rows.

## The slots

A line is not free-form. Each row names a `LocalizeCVGroup` - the moment the line plays at - and the game only plays the slots it knows about. Aru's 46, in the order they appear in her collection:

| Where | Slots |
| --- | --- |
| Formation screen | `Formation_In_1`, `Formation_In_2`, `Formation_Select` |
| Starting a mission | `Tactic_In_1`, `Tactic_In_2` |
| Mission over | `Tactic_Victory_1`, `Tactic_Victory_2`, `Tactic_Defeat_1`, `Tactic_Defeat_2` |
| Entering a fight | `Battle_In_1`, `Battle_In_2` |
| During a fight | `Battle_Shout_1` to `_3`, `Battle_Damage_1` to `_3`, `Battle_Move_1`, `Battle_Move_2`, `Battle_Defense_1`, `Battle_Covered_1`, `Battle_Recovery_1`, `Battle_Buffed_1`, `Battle_BuffSelf_1`, `Battle_TacticalAction_1` |
| Skills | `CommonSkill`, `ExSkill_1` to `_3`, `ExSkill_Level_1` to `_3` |
| Fight over | `Battle_Victory_1`, `Battle_Victory_2`, `Battle_Retire` |
| Relationship | `Relationship_Up_1` to `_4`, `Growup_1` to `_4` |

Numbered variants are random picks, not a sequence. Three `Battle_Damage` lines means she says a different one each time she gets hit, which is most of what stops a character sounding like a soundboard. Two is the shipped minimum for the ones that fire constantly.

You do not have to fill all 46. A student with `Formation_Select`, both `Tactic_In`, the three `ExSkill` lines and a couple of `Battle_Damage` is already a student who talks in every place you actually notice.

## Where the audio lives

Not in the addressables catalog, which is the first surprise. Search it for `audio/voc_jp` and you get nothing, because voice ships down a completely separate path that the Shittim server does not touch.

Every voiced character has one archive per language, sitting in your client install:

```
BlueArchive_Data\StreamingAssets\PUB\Resource\GameData\MediaResources\Audio\VOC_JP\JP_Aru.molru
BlueArchive_Data\StreamingAssets\PUB\Resource\GameData\MediaResources\Audio\VOC_KR\KR_Aru.molru
```

286 of them in the JP folder. The index that maps a path to an archive is its own file, `Catalog\MediaResources\MediaCatalog.bytes`, and it works the same way the addressables catalog does - a lowercase lookup key on the left, the real path on the right:

```
audio/voc_jp/jp_aru/aru_battle_buffed_1  ->  Audio/VOC_JP/JP_Aru/Aru_Battle_Buffed_1.ogg
```

Inside a `.molru` there is a 53-byte header and then the character's clips, concatenated, as ordinary Ogg Vorbis. Aru's is 2.5 MB and holds 76 of them, in the same alphabetical order the catalog lists her paths in. If you want to hear what a clip sounds like before you point a line at it, carving one out is a matter of cutting from one `OggS` to the next and saving it with an `.ogg` extension.

That is also the honest limit of this step. The server rewrites the addressables catalog for you, which is what makes custom art work; it does not rewrite the media catalog. Shipping your own recordings means editing that file and that archive in the client install yourself, and nothing in this toolchain does it for you.

## So point her at clips that exist

Which is not the consolation prize it sounds like, because the paths are just strings and nothing checks that the clip you picked belongs to the student saying it.

Each row carries parallel per-nation lists - `Nation`, `Path`, `Volume`, `Delay` - so one row covers JP and KR at once:

```
Nation:  JP                                        KR
Path:    Audio/VOC_JP/JP_Aru/Aru_Tactic_In_1       Audio/VOC_KR/KR_Aru/Aru_Tactic_In_1
```

Note the path has no extension on it. `Volume` and `Delay` are per-nation too, and `Delay` is the one worth using: a line that fires the instant a skill starts steps on the animation, and shipped rows push a lot of them back a fraction of a second.

The useful move is not to point every line at one character. Pick per slot. Take her battle shouts off one student and her formation lines off another if that is where the right delivery is, and the result is a voice that does not obviously belong to anybody in particular. Keep one character for the lines that appear next to each other, though - `Tactic_In_1` and `Tactic_In_2` in two different voices is immediately audible.

## Writing the subtitles

This is where she gets to sound like herself regardless of whose audio is playing, because the text on screen is entirely yours.

Subtitle rows key off `LocalizeCVGroup` plus `CharacterVoiceGroupId`, so a row is bound to one slot of one student. `TLMID` is a bookkeeping string of the form `10000_10` - character id, underscore, an index. `LocalizeEN` is the line:

```
All right. Let's get to work, team.
```

Write these before you finish choosing clips, not after. If you know what she says, picking a delivery that fits is easy; if you pick clips first you end up writing subtitles to match whatever the donor happened to be feeling.

Only subtitle the lines with words in them. Aru has 46 rows and 33 subtitles, and a subtitle attached to a grunt looks like a bug.

## The collection page

Four fields decide how her voice list on the profile behaves.

`CVCollectionType` is `CVNormal` or `CVEtc` - the two headings the list is split under. `CollectionVisible` decides whether the line shows up there at all. `DisplayOrder` is the sort within a heading. `UnlockFavorRank` gates a line behind a relationship level, and Aru's rows use 0, 1 and 5 - most lines available immediately, a few held back.

Setting `UnlockFavorRank` on all of them to 0 works and looks slightly wrong, because a collection with nothing left to unlock is a collection nobody comes back to. Hold four or five lines at rank 5 the way the shipped students do.

Next: [her own lines](lines.md).

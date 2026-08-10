---
id: lines
title: 'Step 15: her own lines'
---

This one is different from the last two, because the rows already exist. The importer clones `CharacterDialogExcel` and its subtitle table, which means your student already talks in the lobby and the cafe - using the donor's words, with the donor's name in them. Nobody notices a borrowed idle animation. Everybody notices being called the wrong name.

37 rows for Aru, out of 11,466 in the table. Rewriting 37 lines is an afternoon, and it is the single highest return on effort in this whole section.

## Where the lines turn up

`DialogCategory` says which screen a line belongs to:

| Category | When |
| --- | --- |
| `UITitle` | the title screen, if she is the one standing on it |
| `CharacterGet` | the moment you first pull her |
| `UILobby` | tapping her in the lobby |
| `UILobbySpecial` | the lobby lines that need something to be true first |
| `Cafe` | her in the cafe |
| `WeaponGet` | unlocking her unique weapon |

`DialogCondition` narrows it further - `Enter` for the line she says when the screen opens, `Idle` for the ones she cycles through if you leave her alone. `DialogType` is `Talk` for all of Aru's; the other types exist for lines that are an action rather than speech.

Read her cloned rows in that order before you write anything. Seeing which screens are covered and how many idle variants each one gets is faster than any description of it, and it tells you exactly how many lines you owe.

## They belong to a costume, not to her

The field that binds a row to your student is `CostumeUniqueId`, not her character id. Aru's is `1000001`, which is her costume group of 10000 times 100, plus the variant.

The importer rewrote that for you, so her cloned rows point at her own costume already. What it means going forward is that dialog is per outfit: a second costume is a second full set of these rows, and a line written once does not follow her into her swimsuit. This is also why the same student can be quiet in one outfit and chatty in another.

## The fields that connect her lines to everything else

Three of them, and they are what make a line feel authored rather than pasted.

`LocalizeCVGroup` and `VoiceId` point at the voice work from [step 14](voice.md). A lobby line with a voice group attached plays that clip while the subtitle shows. Leaving them empty is fine and gives you a silent line, which is what you want for anything you did not give a clip to.

`AnimationName` is which clip of her memory lobby rig plays while she says it. Aru's rows use `01` and `03` - the Talk animations from [step 10](lobby.md). This is why the lobby rig is worth doing before this step rather than after: with it in place, picking an animation per line is what turns a list of sentences into a character reacting.

`Duration` is how long the line stays on screen. Long line, long duration. Getting this wrong is the most common way a hand-written set of lines feels off - a two-clause sentence that vanishes in the time it takes to read half of it.

## Favor gates

`UnlockFavorRank` holds a line back until her relationship is high enough. Aru's cafe rows sit at 15, which is a long way up.

Use it. A student whose entire dialogue set is available the first time you tap her has no arc, and the shipped students all lean on this - the early lines are polite, the late ones are not. Writing three tiers of a lobby line and gating them at 1, 8 and 15 costs you two extra rows and is the difference between a character and a nameplate.

## Actually writing them

The subtitle table holds the text; the dialog row holds everything about when and how it plays. Edit them as a pair.

Two things worth knowing before you start. Her name appears inside the donor's lines more often than you would guess from reading the list, usually in the `CharacterGet` and `WeaponGet` ones, and those are the lines a player hears at the highest-attention moment there is. Do those first. And the cafe lines are the ones that get read fifty times rather than once, so they are the ones where a line that is slightly too long or slightly too pleased with itself will wear through.

Next: [her MomoTalk](momotalk.md).

---
id: skills
title: 'Step 13: her own skills'
---

Right now she has four skills and they are the donor's, down to the donor's name printed on the card. This step gives her her own set: her own names, her own descriptions, her own costs and cooldowns, sitting on ids nobody else owns.

## What a student's kit actually is

Open Aru and count. She has seven groups of skill rows, not four:

| Group | Rows | Ids | What it is |
| --- | --- | --- | --- |
| `AruNormal01` | 1 | 10000100 | her auto attack |
| `AruEx01` | 10 | 10000200-10000209 | the EX skill, the one that costs cost |
| `AruPublic01` | 10 | 10000300-10000309 | the basic skill |
| `AruGearPublic01` | 10 | 10000350-10000359 | the same skill again, after her gear unlocks it |
| `AruPassive01` | 10 | 10000400-10000409 | the enhanced skill |
| `AruWeaponPassive01` | 10 | 10000450-10000459 | the same again, after her unique weapon |
| `AruExtraPassive01` | 10 | 10000500-10000509 | the sub skill |

Ten rows is ten levels. One row per level, each carrying its own cost, cooldown, icon and text, which is why retuning a skill means editing ten rows rather than one.

The two doubled groups are why the game can show you "Noir Attack" and "Noir Attack+" as if they were different skills. They are different groups, and which one she uses is decided by `CharacterSkillListExcel` - the table the importer already cloned for her. She has exactly four rows in it and they read like a truth table:

| `MinimumGradeCharacterWeapon` | `MinimumTierCharacterGear` | public skill | passive |
| --- | --- | --- | --- |
| 0 | 0 | `AruPublic01` | `AruPassive01` |
| 2 | 0 | `AruPublic01` | `AruWeaponPassive01` |
| 0 | 2 | `AruGearPublic01` | `AruPassive01` |
| 2 | 2 | `AruGearPublic01` | `AruWeaponPassive01` |

Her unique weapon at 2 star swaps the passive, her gear at tier 2 swaps the basic skill, and the auto attack, EX and sub skill are the same in all four. Each row just names the groups that combination should use.

That is the whole trick of this step. `CharacterSkillListExcel` is already hers. The groups it names are not. Point it at groups of your own and she has her own kit.

## The ids are derivable

Skill ids are `characterId * 1000` plus the slot offset. Aru is 10000, so her EX skill starts at 10000200. A student at 10100 gets 10100100 for the auto attack, 10100200 through 10100209 for the EX skill, and so on down the table above.

Group names are just strings and nothing parses them, but every shipped student follows `<DevName><Slot>01`, so match it. `SuzuEx01`, `SuzuGearPublic01`.

## Doing it

For each of the seven groups:

1. Read the donor's rows for that group out of `SkillDBSchema`.
2. Rewrite `GroupId` to your student's group name, and the `Id` on each row to your student's id block.
3. `SkillDataKey` and `VisualDataKey` are both equal to the group id on shipped rows. Leave them pointing at the donor's group - see below.
4. Insert.

Then rewrite her four `CharacterSkillListDBSchema` rows so they name your seven groups instead of the donor's, and she is using her own rows.

At this point nothing has visibly changed, which is the correct result. You have a copy of the donor's kit that belongs to her. Now change it.

## The text

`LocalizeSkillId` on each skill row points into `LocalizeSkillDBSchema`, which is where the name and the description live. Four fields matter there: `Key`, `NameEn`, `DescriptionEn` and `SkillInvokeLocalizeEn` - the last being the shout that appears when the skill fires.

**Every level has its own localize row.** Not one per skill. Aru's EX skill points at ten different keys, one per level, and the ten rows differ only in the numbers inside the sentence:

```
Deals [c][007eff]274%[-][/c] of ATK as damage to 1 enemy.
Also inflicts [c][007eff]292%[-][/c] of ATK as damage to enemies in a circular area.
```

Level 10 of the same skill is that text with 809% and 861% in it. The coefficient you see on the card is written into the string by hand, so the growth curve is something you decide and then type out.

`[c][007eff]` opens a blue span and `[-][/c]` closes it. Look at what is inside the span in that example and what is not: the percentages are blue, the enemy count is not. Blue means "this number changes with the level". Getting that backwards is one of the few things that will make a skill card look wrong to someone who has never modded anything.

The keys themselves are large numbers that look like hashes, and nothing checks them - pick values nobody is using and set the same value on both sides. Sixty-one skill rows means sixty-one localize rows, which is the real labour of this step. Decide the curve first, then generate the text off it rather than writing sixty-one sentences by hand.

## Icons

You do not need to draw one. `IconName` is a full path and shipped skills point it at shared commons:

```
UIs/02_Tactics/02_SkillIcon/COMMON_SKILLICON_CIRCLE
UIs/02_Tactics/02_SkillIcon/COMMON_SKILLICON_TARGET
UIs/02_Tactics/02_SkillIcon/COMMON_SKILLICON_WEAPONBUFF
```

There are a couple of dozen of them and Aru uses three. Pick whichever reads closest to what your skill does. A custom icon is possible through the bundle route from [step 9](own-assets.md), but the commons are what the shipped kit uses, so this is not the thing that will make her look homemade.

## What you cannot change with rows alone

`SkillDataKey` is the address of the skill's actual behaviour - what it targets, how it moves, what comes out of the gun. That is an asset, not a row, so pointing your rows at the donor's key means your student's skill *does* what the donor's did, however you have named and described it.

This is less limiting than it sounds, because the donor was chosen in [step 1](donor.md) partly for having a kit that suits her. Pick your seven source groups from different characters if one donor's kit does not fit - nothing requires all seven to come from the same student, and mixing an EX skill from one and a sub skill from another is a legitimate way to build a kit that is genuinely hers. Take the numbers seriously while you do it: a skill described as hitting one enemy that visibly hits three is the kind of thing people notice immediately.

Next: [her voice](voice.md).

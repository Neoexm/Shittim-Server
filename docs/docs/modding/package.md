---
id: package
title: 'Step 2: write her data'
---

Her name, her school, her rarity, her stats and her whole profile page are one text file called `character.json`, and the mod is a zip with that file somewhere in it. You will come back and edit this file more than once, so treat the first pass as a draft.

The smallest possible mod:

```json
{
  "name": "Shirakami Suzu",
  "donorId": 10000,
  "DevName": "Suzu",
  "School": "Millennium",
  "Club": "GameDev",
  "Rarity": "SSR",
  "FullNameEn": "Shirakami Suzu",
  "FamilyNameEn": "Shirakami",
  "PersonalNameEn": "Suzu",
  "StatusMessageEn": "five more minutes",
  "SchoolYearEn": "1st year",
  "CharacterAgeEn": "16",
  "BirthdayEn": "March 3rd",
  "CharHeightEn": "154cm",
  "HobbyEn": "sleeping through homeroom",
  "ProfileIntroductionEn": "A first-year who has never once been awake for the register.",
  "MaxHP1": 4200,
  "MaxHP100": 42000,
  "AttackPower1": 380,
  "AttackPower100": 3800
}
```

Two keys are instructions to the importer:

| Key | What it does |
| --- | --- |
| `name` | her display name. Gets written into a fresh name row in all five languages |
| `donorId` | who to copy. Fills in the donor picker, and you can still change it there |

**Everything else is an override.** Each key gets matched against three lists of allowed fields and applied to whichever row owns it. A key that matches nothing is ignored silently, so a typo just does nothing rather than telling you about it.

The file can sit anywhere in the zip, at any depth. A zip with no manifest at all is fine too - the name falls back to the zip's filename and you pick the donor in the dialog.

## Fields about the character herself

| Field | Values |
| --- | --- |
| `DevName` | internal name, never shown to players |
| `Rarity` | `N`, `R`, `SR`, `SSR` |
| `School` | `Abydos`, `Gehenna`, `Millennium`, `Trinity`, `Hyakkiyako`, `SRT`, `Arius`, `Shanhaijing`, `RedWinter`, `Valkyrie`, `ETC` |
| `Club` | the donor's is a fine default if you do not care |
| `SquadType` | `Main` or `Support` (striker or special) |
| `TacticRole` | `DamageDealer`, `Tanker`, `Supporter`, `Healer`, `Vehicle` |
| `WeaponType` | `AR`, `SMG`, `SR`, `HG`, `SG`, `MG`, `RL`, `RG`, `MT`, `FT` |
| `BulletType` | `Explosion`, `Pierce`, `Mystic`, `Sonic` |
| `ArmorType` | `LightArmor`, `HeavyArmor`, `Unarmed`, `ElasticArmor` |
| `DefaultStarGrade` | what she arrives at |
| `MaxStarGrade` | 3 normally, 5 for the ones who go past it |

Any of these takes either the name or the raw number. Names are not case sensitive.

Changing `WeaponType` here changes the icon and the label, not the gun she actually shoots - that comes from her weapon row, which is the donor's.

## Fields about her profile

These are the text on her profile page. All sixteen are the English versions; the other languages keep the donor's text unless you edit the database yourself.

`FullNameEn`, `FamilyNameEn`, `PersonalNameEn`, `StatusMessageEn`, `SchoolYearEn`, `CharacterAgeEn`, `BirthDay`, `BirthdayEn`, `CharHeightEn`, `HobbyEn`, `DesignerNameEn`, `IllustratorNameEn`, `CharacterVoiceEn`, `WeaponNameEn`, `WeaponDescEn`, `ProfileIntroductionEn`

`BirthDay` is the sortable version the game uses internally and `BirthdayEn` is the words it prints, so set both or the birthday list sorts her wrong.

`ProfileIntroductionEn` is the paragraph of bio and it is the one worth actually writing.

## Fields about her stats

`MaxHP1`, `MaxHP100`, `AttackPower1`, `AttackPower100`, `DefensePower1`, `DefensePower100`, `HealPower1`, `HealPower100`, `CriticalPoint`, `DodgePoint`, `AccuracyPoint`, `Range`, `AmmoCount`, `AmmoCost`

The `1` and `100` pairs are the two ends of her growth curve and the game works out everything in between. Change one without the other and you get a curve that goes the wrong way, so always do them as a pair. A reasonable rule of thumb is that level 100 is roughly ten times level 1 for the big three.

## Putting art in the zip

Files with these extensions get pulled out and kept: `.png` `.jpg` `.jpeg` `.bundle` `.skel` `.atlas` `.json` `.bytes` `.ogg` `.wav` `.mp4`

They get copied, flat, into a folder next to the mod registry. Folder structure inside the zip is thrown away and only the filename survives, so two files with the same name in different folders will clash.

They are **not installed**. Unless the manifest names a `bundle` they sit there as a holding area and she keeps drawing the donor's art. Steps 5 to 8 are what you do with them, and [step 9](own-assets.md) covers `bundle` and the `addressables` list, which are what put art in the game under a name of your own.

Next: [install her](import.md).

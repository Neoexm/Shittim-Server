---
id: own-assets
title: 'Step 9: ship the art under her own name'
---

Step 8 puts your art into somebody else's file, under somebody else's name. It works and it is the fastest route, but it has two costs: your student's files are entangled with the donor's, and if the donor ever gets updated your art goes with it.

This page is the other route. You build one Unity asset bundle containing your own files, under names you chose, and the server hands the game an index that says those names exist. Nothing gets overwritten and nothing is shared with the donor.

It is more setup than step 8 - you need Unity once - but after that it is the easier of the two to live with, because adding a file is adding a file rather than surgery on a 60 MB archive.

## How the game finds a file

Every asset the game can load is in an index, and the index maps a short lookup name to a real file inside a real bundle. Two examples, taken straight out of the shipped index:

| What the game asks for | Where it actually lives |
| --- | --- |
| `uis/01_common/01_character/student_portrait_aru` | `Assets/_MX/AddressableAsset/UIs/01_Common/01_Character/Student_Portrait_Aru.png` |
| `assets/_mx/spinecharacters/aru_spr/aru_spr.png` | `Assets/_MX/SpineCharacters/aru_spr/aru_spr.png` |

The lookup name is not invented, it is derived from the file path by two rules:

- anything under `Assets/_MX/AddressableAsset/` loses that prefix and its file extension
- anything else keeps its whole path

and both get lowercased. That is the entire naming convention, it holds for all 138,000-odd assets in the index, and the server applies the same two rules to yours. **So you never type a lookup name anywhere. You put the file at the right path and the name follows.**

## What asks for those names

Her costume row. It is one of the rows the importer clones for you, and it is where the asset names live:

| Field | What it draws | Aru's value |
| --- | --- | --- |
| `TextureDir` | the portrait on the student list and in formation | `UIs/01_Common/01_Character/Student_Portrait_Aru` |
| `CollectionTexturePath` | the big illustration on her profile page | `UIs/01_Common/14_CharacterCollect/Student_Portrait_Aru_Collection` |
| `CollectionBGTexturePath` | the background behind it | `UIs/01_Common/14_CharacterCollect/BG_Gehenna_Collection` |
| `SpineResourceName` | her talking sprite in story scenes | `UIs/03_Scenario/02_Character/CharacterSpine_aru` |
| `ModelPrefabName` | the 3D battle model | `Aru_Original` |
| `AnimatorName` | which animation set that model uses | `Aru_Original_Controller` |
| `InformationPacel` | the enemy-info card | `UIs/01_Common/21_EnemyInfo/EnemyInfo_Aru_SR` |
| `TextureBoss` | the boss health bar portrait | `UIs/02_Tactics/05_CampaignBoss/Champion_Hpbar_Portrait_Aru` |
| `CombatStyleTexturePath` | the combat style panel | usually blank |

Compare those against the table above and the shape is obvious: `TextureDir` **is** the lookup name, spelled with its original capitals. Set it to `UIs/01_Common/01_Character/Student_Portrait_Nx0001` and the game will ask for `uis/01_common/01_character/student_portrait_nx0001`, which is a name you can now supply.

A few of these have suffixes the game appends on its own. `TextureDir` plus `_Small` is the small portrait, so shipping `Student_Portrait_Nx0001.png` without `Student_Portrait_Nx0001_Small.png` gets you a good portrait in one place and a blank square in another.

## Get Unity

You need the same Unity the client was built with, which is **2021.3.56f2**. A different patch release will usually load, a different minor version usually will not.

You can check this yourself rather than trusting the number: open any file in `BlueArchive_Data\StreamingAssets\PUB\Resource\GameData\Windows\` in a text editor and look at the first line. Bundles start with `UnityFS` and the editor version in plain text a few bytes later.

Install it from the Unity Hub, make an empty 3D project, and add the **Addressables** package from Window > Package Manager. That is the whole setup and you only do it once.

## Put your files where their names come from

In the Unity project, recreate the folder path the name implies. For a portrait that is:

```
Assets/_MX/AddressableAsset/UIs/01_Common/01_Character/
```

and drop `Student_Portrait_Nx0001.png` in it. The folders are what produce the name, so a typo in a folder is a typo in the name.

For a spine rig it is `Assets/_MX/SpineCharacters/nx0001_spr/`, holding the same six files the shipped rigs have - `nx0001_spr.png`, `nx0001_spr.atlas.txt`, `nx0001_spr.skel.bytes`, and the three assets the spine-unity importer generates from them (`_Atlas.asset`, `_Material.mat`, `_SkeletonData.asset`).

Set the import settings on textures to match what you are replacing - portraits are sprites, spine sheets are plain textures with alpha, and none of them want mipmaps.

Then select each file and tick **Addressable** in the inspector. The address Unity fills in does not matter and never travels; the server writes the lookup name from the file path. Ticking the box is only what makes Unity include the file in a bundle.

## Build the bundle

Window > Asset Management > Addressables > Groups. Your files will be in a default group. Build > New Build > Default Build Script.

The bundle comes out under `Library/com.unity.addressables/aa/Windows/` (or `ServerData/` if you set the group to remote), named something like `defaultlocalgroup_assets_all_<hash>.bundle`. Rename it to something you will recognise - `nx0001_assets_all.bundle` - and that is the file you ship.

**Anything your files depend on comes along automatically**, which is mostly what you want. A spine material depends on the spine shader, so Unity pulls the shader into your bundle and your rig renders without needing anything the game already has. It also means a bundle can get bigger than the files you put in it. A plain PNG portrait has no dependencies at all, which is why it is the right thing to try first.

## Tell the mod what is inside it

Two extra keys in `character.json`, next to the ones from [step 2](package.md):

```json
{
  "name": "Shirakami Suzu",
  "donorId": 10000,
  "bundle": "nx0001_assets_all.bundle",
  "addressables": [
    "Assets/_MX/AddressableAsset/UIs/01_Common/01_Character/Student_Portrait_Nx0001.png",
    "Assets/_MX/AddressableAsset/UIs/01_Common/01_Character/Student_Portrait_Nx0001_Small.png",
    "Assets/_MX/AddressableAsset/UIs/01_Common/14_CharacterCollect/Student_Portrait_Nx0001_Collection.png"
  ],
  "TextureDir": "UIs/01_Common/01_Character/Student_Portrait_Nx0001",
  "CollectionTexturePath": "UIs/01_Common/14_CharacterCollect/Student_Portrait_Nx0001_Collection"
}
```

`bundle` names the bundle file, which goes in the zip alongside the manifest. `addressables` lists the files inside it, written as the paths they had in the Unity project - which you already know, because you chose them.

The list form guesses each file's type from its extension, which is right for textures, spine files and prefabs. If you need to be explicit, write it as an object instead and give the Unity type as the value:

```json
"addressables": {
  "Assets/_MX/SpineCharacters/nx0001_spr/nx0001_spr_Atlas.asset": "Spine.Unity.SpineAtlasAsset"
}
```

The costume fields in that same file - `TextureDir` and the rest of the table above - are overrides like any other manifest key, and they are the half that makes the game ask. Ship a bundle without them and your files are in the index, correctly named, and nothing ever looks them up.

## Install and check

Import the zip the usual way ([step 3](import.md)), restart the server, then restart the client.

On the first launch after install the client downloads a fresh index, which is about 64 MB and takes a few seconds on the loading screen. That only happens when the index has changed, so it is once per install, not once per launch. The server does not advertise the index at all until some installed mod names a bundle, so a server with no bundled mods costs nothing.

## When it does not work

**She still has the donor's art.** The costume fields did not get set. Check the spelling against the table - `TextureDir` is case sensitive as a key even though the value's case does not matter.

**Blank square, or the placeholder question mark.** The name is being asked for and the index does not have it. Nearly always a mismatch between the folder path in Unity and the value you put in the costume field. Read them side by side, apply the two rules by hand, and see where they diverge.

**Nothing at all changed, no download on launch.** Either no installed mod names a `bundle`, or the bundle named in the manifest did not make it into the zip. The server logs a line naming the mod when a bundle it expects is missing.

**She loads but the sprite is untextured or pink.** The material's shader did not travel. Rebuild the bundle with the material selected as addressable rather than just the texture, so Unity walks the dependency.

**The client hangs on the loading screen after install.** The index it downloaded is not one it can read, which in practice means the source index the server rewrote came from a different client version than the one you are running. Delete `catalog_Remote.bytes` from `%USERPROFILE%\AppData\LocalLow\NEXON Games\Blue Archive\`, launch the retail client once to fetch a current one, and reinstall the mod.

## Where to go from here

Portraits first, because they are one file with no dependencies and you find out quickly whether your names are right. Then the collection illustration, then the scenario spine, then the menu rig. Each one is the same five minutes once the project exists.

After that the project you have set up here is also what steps 10 and 11 need, so [the memory lobby](lobby.md) is the next thing to build in it.

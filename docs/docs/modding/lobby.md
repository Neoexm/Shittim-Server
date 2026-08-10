---
id: lobby
title: 'Step 10: the memory lobby'
---

The memory lobby is the full-screen scene you get from her profile, where she stands there and you can talk to her and pat her head. It is the natural thing to do after the student menu, because it is one more Spine rig and nothing else - no 3D, no new tables, no new tools past what [step 9](own-assets.md) already had you install.

Your student already has a memory lobby. The importer clones the row, so what she currently has is the donor's scene with the donor's art. This step replaces it with your own.

## What it is made of

Three things:

**A row** in `MemoryLobbyExcel`, already cloned for you, which points at the other two.

**A prefab** at `Assets/_MX/AddressableAsset/UI/UILobbyElement/Lobby<Name>.prefab`. This is the scene - it holds the rig, the camera framing and the touch areas.

**A Spine rig** at `Assets/_MX/SpineLobbies/<Name>_home/`. Same file types as the menu rig from steps 5 to 8, but drawn much larger and with a lot more animation.

The row's fields, with Aris's real values:

| Field | What it does | Aris |
| --- | --- | --- |
| `PrefabName` | the scene prefab, minus its folder and extension | `LobbyAris` |
| `RewardTextureName` | the thumbnail on the reward and the lobby list | `UIs/01_Common/08_Lobbyillust/Lobbyillust_Icon_Aris_01` |
| `SlotTextureName` | an alternative thumbnail, usually blank | blank |
| `BGMId` | which track plays, from `BGMExcel` | `67` |
| `MemoryLobbyCategory` | which tab it appears under | `UILobbySpecial` |

`PrefabName` follows the short-name rule from step 9: `LobbyAris` becomes the lookup `ui/uilobbyelement/lobbyaris`, and the file sits at `Assets/_MX/AddressableAsset/UI/UILobbyElement/LobbyAris.prefab`. `RewardTextureName` is a full path because it lives further down. Both are just addresses and you set them the same way as the costume fields:

```json
{
  "PrefabName": "LobbyNx0001",
  "RewardTextureName": "UIs/01_Common/08_Lobbyillust/Lobbyillust_Icon_Nx0001_01",
  "BGMId": 67
}
```

Leave `BGMId` on the donor's until you have a reason not to. It points at a track that already exists, and pointing it at one that does not gets you silence.

## The rig

Two shapes ship in the game and you want the simpler one.

**Flat** - everything directly in `Assets/_MX/SpineLobbies/Aris_home/`: the skeleton, the atlas, two or three texture pages, a material per page, and the two generated assets.

```
Assets/_MX/SpineLobbies/Nx0001_home/
  Nx0001_home.skel.bytes
  Nx0001_home.atlas.txt
  Nx0001_home.png
  Nx0001_home2.png
  Nx0001_home_Nx0001_home.mat
  Nx0001_home_Nx0001_home2.mat
  Nx0001_home_Atlas.asset
  Nx0001_home_SkeletonData.asset
```

**Nested**, like Aru, which puts the student in `Aru_home/Aru_home/` and a second rig for the background in `Aru_home/Aru_Scene/`, plus a timeline. That is for lobbies where the scenery animates too. Skip it. A static background in the prefab looks fine and is one file.

More than one texture page is normal here and not a mistake - the lobby rig is big enough that a 2048 sheet runs out, so the exporter spills onto a second and third. One material per page, named `<rig>_<page>.mat`.

## The animations

This is the part with actual volume, and it is a fixed list rather than an open-ended one. Each animation in your Spine file gets a small wrapper asset next to the rig, named `<rig>-<animation>.asset`. Aru's set:

| Animation | When it plays |
| --- | --- |
| `Start_Idle_01` | the entry, once, when the lobby opens |
| `Idle_01` | the loop she sits in |
| `Talk_01_A` through `Talk_06_A`, each with a matching `_M` | her dialogue lines |
| `Look_01_A` / `Look_01_M`, `LookEnd_01_A` / `LookEnd_01_M` | reacting to being looked at |
| `Pat_01_A` / `Pat_01_M`, `PatEnd_01_A` / `PatEnd_01_M` | head pats |

The `_A` and `_M` pairs always ship together and the game expects both, so build them in pairs even if one of the two barely moves.

You do not need all of them to see something. A rig with only `Start_Idle_01` and `Idle_01` opens, plays and loops - she just does not react. That is the right first build, because it tells you your names and your prefab are correct before you spend a week animating head pats.

## Building it

Same as step 9 and it goes in the same bundle. Import the spine files with the spine-unity runtime, which generates the `_Atlas`, `_Material` and `_SkeletonData` assets for you and creates the animation wrappers when you ask it to. Build the prefab with a `SkeletonGraphic` pointing at your `_SkeletonData`, frame it, save it under `Assets/_MX/AddressableAsset/UI/UILobbyElement/`, tick everything Addressable and rebuild.

Then list the files in `addressables` in the manifest, exactly as in step 9. The prefab is `UnityEngine.GameObject`; the guesser gets that from `.prefab` on its own.

## When it does not work

**The old lobby still opens.** `PrefabName` did not take, or it took and the name does not resolve, in which case the game falls back. Check the address by hand: drop `Assets/_MX/AddressableAsset/`, drop `.prefab`, lowercase.

**Black screen with the UI over it.** The prefab loaded and the rig inside it did not. The prefab references the rig by GUID rather than by address, so this is nearly always the rig assets not being marked Addressable - Unity then leaves them out of the bundle and the reference dangles.

**She plays her entry and freezes.** No `Idle_01`, or it is named something else. The animation name inside the Spine file is what matters, not the name of the wrapper asset.

Next: [the battle model](battle.md), which is the last tier and less of a wall than it looks.

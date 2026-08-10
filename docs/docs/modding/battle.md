---
id: battle
title: 'Step 11: the battle model'
---

Everything up to here has been 2D. In combat the student is a rigged 3D character, which is why this tier gets described as the wall.

It is real work, but it is much more bounded than it sounds, and the two facts that make it bounded are worth knowing before you decide:

**The animator is an override, not a state machine.** You are not authoring the logic of when she reloads or how a kneel blends into a stand. That lives in a shared base controller that every student uses. Yours is an `.overrideController`, which is a list of slots you drop clips into. The game ships 1,665 of them.

**The clip list is fixed and short.** Thirty-four clips, with names that are completely mechanical. Not sixty-eight, not open-ended, and every one of them is a name you can look up rather than guess.

## The thirty-four clips

All named `<ModelPrefabName>_<State>.anim`, so for a model called `Nx0001_Original` the first is `Nx0001_Original_Normal_Idle.anim`. Aru's set, grouped:

| Group | States |
| --- | --- |
| Normal stance | `Normal_Idle`, `Normal_Reload`, `Normal_Callsign`, `Normal_Attack_Start`, `Normal_Attack_Ing`, `Normal_Attack_Delay`, `Normal_Attack_End` |
| Kneeling stance | the same seven with `Kneel_` |
| Moving | `Move_Ing`, `Move_Callsign`, `Move_End_Normal`, `Move_End_Kneel` |
| Getting hurt | `Vital_Panic`, `Vital_Dying_Ing`, `Vital_Death`, `Vital_Retreat` |
| Winning | `Victory_Start`, `Victory_End` |
| Cafe | `Cafe_Idle`, `Cafe_Walk`, `Cafe_Reaction` |
| Formation screen | `Formation_Idle`, `Formation_Pickup` |
| EX skill | `Exs`, `Exs_Cam`, `Exs_Cutin` |
| Other skills | `Public01`, `TSS_Interaction01` |

`_Start`, `_Ing`, `_Delay`, `_End` is the attack broken into four, and `_Ing` is the looping middle. `Exs_Cam` animates the camera rather than the character, which is why the EX skill swings around her.

Half of these barely move. `Normal_Callsign` is a raised hand. `Formation_Idle` is a breathing loop. If you are starting out, make `Normal_Idle` properly and copy it into the ones you have not done yet - she will look stiff and she will work.

## The rest of the folder

Everything for one student lives in `Assets/_MX/Characters/<Model>/`. Aru has 74 assets in there:

| Folder | Count | What |
| --- | --- | --- |
| `Animation/` | 34 | the clips above |
| `Model/` | 4 | two FBX files, the body mesh and the halo, plus two outline meshes |
| `Model/Material/` | 7 | body, face, eyebrow, eye-mouth, hair, halo, weapon |
| `Model/Texture/` | 12 | PSDs for each of those, several with a `_Mask` variant |
| `Effects/Prefab/` | 11 | skill effects and projectiles |
| `Audio_<name>/` | 5 | skill sound effects |
| `AnimationAudioEvents/` | 2 | the hooks that fire a voice line on death and victory |

The material split is a convention, not a requirement, but it is worth following: the game's shading expects face and hair to be separate so it can light them differently.

## The addressable side

Only thirteen names, in `Assets/_MX/AddressableAsset/Character/<Model>/`:

```
Nx0001_Original.prefab                     the model itself
Nx0001_Original_Controller.overrideController
Nx0001_Original_Mesh.controller
Nx0001_Original_Public1.playable           skill timelines
Nx0001_Original_GearPublic1.playable
Nx0001_Original_EX1.playable
Nx0001_Original_EX1_CutIn.playable
Cafe/Cafe_Nx0001_Original.prefab
Cafe/Cafe_Nx0001_Original_Controller.overrideController
Echelon/Echelon_Nx0001_Original.prefab
Echelon/Echelon_Nx0001_Original_Controller.overrideController
Strategy/Strategy_Nx0001_Original.prefab
Strategy/Strategy_Nx0001_Original_Controller.overrideController
```

The other 74 assets are pulled in as dependencies of these, so they do not need naming - they only need to be in the bundle, which they will be if the prefabs reference them.

Two costume fields point at this:

```json
{
  "ModelPrefabName": "Nx0001_Original",
  "AnimatorName": "Nx0001_Original_Controller"
}
```

The cafe, echelon and strategy prefabs get no field of their own. Aru's `CafeModelPrefabName`, `EchelonModelPrefabName` and `StrategyModelPrefabName` are all blank, because the game builds those names out of `ModelPrefabName` by convention. Name the folders and files the way the list above does and they resolve on their own. Set the fields only if you want to point somewhere else.

## A sensible order

**Get her standing.** Model, one material, `Normal_Idle`, the prefab, the override controller with that one clip in every slot. Set `ModelPrefabName` and `AnimatorName`, build, deploy. She will be in battle, sliding around in a T-pose-adjacent idle, and everything else is filling in slots.

**Then the stances.** The fourteen normal and kneel clips are what you actually watch during a fight.

**Then movement and vitals.** Eight more, and after these she stops looking broken.

**Then the EX skill.** `Exs`, `Exs_Cam`, `Exs_Cutin` plus the effect prefabs is a project in itself and it is the flashiest part, so it is a reasonable thing to save for when the rest works.

**Cafe and formation last.** Five clips, low stakes, nobody is watching closely.

## When it does not work

**She is invisible in battle but the health bar is there.** `ModelPrefabName` resolves to nothing. The address is `character/<model>/<model>` lowercased - check it by hand.

**She is there and in a T-pose.** `AnimatorName` did not resolve, or the override controller has empty slots. An empty slot falls through to the base clip, which is nothing.

**She animates but the weapon floats beside her.** The weapon is a separate mesh parented to a bone in the FBX. Re-export with it bound.

**Everything works and she is bright pink.** The materials did not travel. Same cause as always - mark them Addressable so Unity walks the dependency into the bundle.

That is everything she looks like. What is left is everything she inherited from the donor and has not stopped inheriting yet, starting with [editing her tables by hand](beyond.md).

---
id: packing
title: 'Step 8: put it in the game'
---

The sheet has to go back into the bundle it came out of.

## Repacking the bundle

**AssetStudio cannot do this.** It extracts only. The tools that write are UABEA (UABE Avalonia - Plugins, Edit Texture, Load PNG) or a Python script using UnityPy.

The script version is: open the bundle, find the texture by name, assign your image to it, save the object, save the bundle, write the bytes back out.

Three rules, all of which break things silently if you ignore them:

- **Do not change the dimensions.** 2048 x 2048 in, 2048 x 2048 out.
- **Do not change the texture format.**
- **Do not create a new object.** Everything referencing that texture finds it by an internal id number, so a fresh object breaks the link to the material and you get an invisible character.

**Always patch from a backup copy, never from the live file.** Otherwise each pass re-encodes the previous pass's output and quality degrades every time.

One thing to know: UnityPy tends to write the texture back uncompressed rather than in the game's original compressed format. It renders fine but the bundle gets several times larger - one real case went from 1.28 MB to 8.39 MB. The index records the original size, so if anything ever checks, that is the mismatch it finds. Not worth solving unless it actually causes a problem.

## If you are replacing the skeleton too

All three files must stay together and keep matching base names. The layout file names its texture on its first line, and the skeleton refers to regions by the names in the layout. Rename one and the whole thing fails to load.

A note on extensions: inside the bundles these files are called `.atlas.txt` and `.skel.bytes` rather than `.atlas` and `.skel`. That is a Unity requirement, not corruption - Unity's text importer only accepts certain extensions, so the convention is to append one. The bytes underneath are exactly what Spine exports. Strip the suffix before opening them in the Spine editor, and put it back before packing.

If you extract a skeleton with a script, be careful to read it as raw bytes. It is a binary file, and reading it as text quietly corrupts any byte that is not valid text - one probe lost 255 bytes out of 25,019 that way.

## The three icons

Separate from the sheet, and easy.

| File | Size |
| --- | --- |
| `Student_Portrait_<Name>.png` | 252 x 204 |
| `Student_Portrait_<Name>_Small.png` | 182 x 144 |
| `Student_Portrait_<Name>_Collection.png` | 404 x 456 |

All three are crops of the illustration, so you can cut them in one pass.

Head and shoulders, scaled to each aspect ratio, is the standard framing. If you want a proper card rather than a plain crop, open a donor's icon alongside yours and match where her eyes sit in the frame - that is most of what makes the shipped ones look composed.

The collection background can be any existing one - nobody will notice.

One warning: the icon bundles are **shared**. One of them holds nearly 1,500 files, every character's icons together. Target by texture name so you only touch hers, and back the bundle up first.

That is the menu tier finished. If you would rather not keep doing surgery on the donor's archives, [step 9](own-assets.md) is the same result out of a bundle of your own, and it is what steps 10 and 11 build on.
